using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace EXE.Services;

public class GHNService : IGHNService
{
    private readonly GHNSettings _settings;
    private readonly HttpClient _httpClient;

    public GHNService(IOptions<GHNSettings> settings, HttpClient httpClient)
    {
        _settings = settings.Value;
        _httpClient = httpClient;
    }

    public async Task<decimal> CalculateFeeAsync(decimal insuranceValue, int toDistrictId, string toWardCode, int itemCount)
    {
        if (string.IsNullOrWhiteSpace(_settings.Token) ||
            _settings.ShopId <= 0 ||
            _settings.FromDistrictId <= 0 ||
            toDistrictId <= 0 ||
            string.IsNullOrWhiteSpace(toWardCode))
        {
            return _settings.FallbackFee;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, _settings.FeeUrl);
        request.Headers.Add("Token", _settings.Token);
        request.Headers.Add("ShopId", _settings.ShopId.ToString());

        var quantity = Math.Max(1, itemCount);
        request.Content = JsonContent.Create(new GHNFeeRequest
        {
            FromDistrictId = _settings.FromDistrictId,
            FromWardCode = _settings.FromWardCode,
            ServiceTypeId = _settings.ServiceTypeId,
            ToDistrictId = toDistrictId,
            ToWardCode = toWardCode,
            Height = _settings.DefaultHeight,
            Length = _settings.DefaultLength,
            Width = _settings.DefaultWidth,
            Weight = Math.Max(1, _settings.DefaultWeight * quantity),
            InsuranceValue = (int)Math.Round(Math.Max(0, insuranceValue), 0, MidpointRounding.AwayFromZero)
        });

        using var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return _settings.FallbackFee;
        }

        var result = await response.Content.ReadFromJsonAsync<GHNFeeResponse>();
        return result?.Data?.Total ?? _settings.FallbackFee;
    }

    public async Task<GHNCreateOrderResult> CreateOrderAsync(GHNCreateOrderInput input)
    {
        if (string.IsNullOrWhiteSpace(_settings.Token) ||
            _settings.ShopId <= 0 ||
            input.ToDistrictId <= 0 ||
            string.IsNullOrWhiteSpace(input.ToWardCode) ||
            string.IsNullOrWhiteSpace(input.ToName) ||
            string.IsNullOrWhiteSpace(input.ToPhone) ||
            string.IsNullOrWhiteSpace(input.ToAddress))
        {
            return new GHNCreateOrderResult
            {
                Success = false,
                Message = "Thieu cau hinh GHN hoac thong tin nguoi nhan."
            };
        }

        var items = input.Items
            .Where(x => x.Quantity > 0)
            .Select(x => new GHNCreateOrderItemRequest
            {
                Name = string.IsNullOrWhiteSpace(x.Name) ? "San pham" : x.Name.Trim(),
                Code = x.Code,
                Quantity = x.Quantity,
                Price = (int)Math.Round(Math.Max(0, x.Price), 0, MidpointRounding.AwayFromZero),
                Length = _settings.DefaultLength,
                Width = _settings.DefaultWidth,
                Height = _settings.DefaultHeight,
                Weight = Math.Max(1, _settings.DefaultWeight)
            })
            .ToList();

        if (!items.Any())
        {
            return new GHNCreateOrderResult { Success = false, Message = "Don hang khong co san pham de tao van don." };
        }

        var quantity = items.Sum(x => x.Quantity);
        using var request = new HttpRequestMessage(HttpMethod.Post, _settings.CreateOrderUrl);
        request.Headers.Add("Token", _settings.Token);
        request.Headers.Add("ShopId", _settings.ShopId.ToString());
        request.Content = JsonContent.Create(new GHNCreateOrderRequest
        {
            PaymentTypeId = _settings.PaymentTypeId,
            Note = input.Note,
            RequiredNote = string.IsNullOrWhiteSpace(_settings.RequiredNote) ? "KHONGCHOXEMHANG" : _settings.RequiredNote,
            FromName = EmptyToNull(_settings.FromName),
            FromPhone = EmptyToNull(_settings.FromPhone),
            FromAddress = EmptyToNull(_settings.FromAddress),
            FromWardName = EmptyToNull(_settings.FromWardName),
            FromDistrictName = EmptyToNull(_settings.FromDistrictName),
            FromProvinceName = EmptyToNull(_settings.FromProvinceName),
            ReturnPhone = EmptyToNull(_settings.FromPhone),
            ReturnAddress = EmptyToNull(_settings.FromAddress),
            ReturnDistrictId = _settings.FromDistrictId > 0 ? _settings.FromDistrictId : null,
            ReturnWardCode = EmptyToNull(_settings.FromWardCode),
            ClientOrderCode = input.ClientOrderCode,
            ToName = input.ToName.Trim(),
            ToPhone = input.ToPhone.Trim(),
            ToAddress = input.ToAddress.Trim(),
            ToDistrictId = input.ToDistrictId,
            ToWardCode = input.ToWardCode.Trim(),
            CodAmount = 0,
            Content = string.Join(", ", items.Select(x => x.Name).Take(5)),
            Weight = Math.Max(1, _settings.DefaultWeight * Math.Max(1, quantity)),
            Length = _settings.DefaultLength,
            Width = _settings.DefaultWidth,
            Height = _settings.DefaultHeight,
            InsuranceValue = (int)Math.Round(Math.Min(Math.Max(0, input.InsuranceValue), 5000000m), 0, MidpointRounding.AwayFromZero),
            ServiceTypeId = _settings.ServiceTypeId,
            PickShift = _settings.PickupShiftIds?.Length > 0 ? _settings.PickupShiftIds : null,
            Items = items
        });

        using var response = await _httpClient.SendAsync(request);
        GHNCreateOrderResponse? result = null;
        try
        {
            result = await response.Content.ReadFromJsonAsync<GHNCreateOrderResponse>();
        }
        catch
        {
            // GHN sometimes returns plain text for gateway errors.
        }

        if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(result?.Data?.OrderCode))
        {
            return new GHNCreateOrderResult
            {
                Success = false,
                Message = result?.Message ?? $"GHN tra ve HTTP {(int)response.StatusCode}."
            };
        }

        return new GHNCreateOrderResult
        {
            Success = true,
            OrderCode = result.Data.OrderCode,
            Message = result.Message,
            TotalFee = result.Data.TotalFee
        };
    }

    public async Task<List<GHNLocationItem>> GetProvincesAsync()
    {
        using var request = CreateMasterDataRequest(HttpMethod.Get, _settings.ProvinceUrl);
        using var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return new List<GHNLocationItem>();

        var result = await response.Content.ReadFromJsonAsync<GHNProvinceResponse>();
        return result?.Data?
            .OrderBy(x => x.ProvinceName)
            .Select(x => new GHNLocationItem { Id = x.ProvinceId, Name = x.ProvinceName ?? "" })
            .ToList() ?? new List<GHNLocationItem>();
    }

    public async Task<List<GHNLocationItem>> GetDistrictsAsync(int provinceId)
    {
        using var request = CreateMasterDataRequest(HttpMethod.Post, _settings.DistrictUrl);
        request.Content = JsonContent.Create(new { province_id = provinceId });
        using var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return new List<GHNLocationItem>();

        var result = await response.Content.ReadFromJsonAsync<GHNDistrictResponse>();
        return result?.Data?
            .OrderBy(x => x.DistrictName)
            .Select(x => new GHNLocationItem { Id = x.DistrictId, Name = x.DistrictName ?? "" })
            .ToList() ?? new List<GHNLocationItem>();
    }

    public async Task<List<GHNLocationItem>> GetWardsAsync(int districtId)
    {
        using var request = CreateMasterDataRequest(HttpMethod.Post, _settings.WardUrl);
        request.Content = JsonContent.Create(new { district_id = districtId });
        using var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return new List<GHNLocationItem>();

        var result = await response.Content.ReadFromJsonAsync<GHNWardResponse>();
        return result?.Data?
            .OrderBy(x => x.WardName)
            .Select(x => new GHNLocationItem { Code = x.WardCode ?? "", Name = x.WardName ?? "" })
            .ToList() ?? new List<GHNLocationItem>();
    }

    private HttpRequestMessage CreateMasterDataRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        if (!string.IsNullOrWhiteSpace(_settings.Token))
        {
            request.Headers.Add("Token", _settings.Token);
        }

        return request;
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed class GHNFeeRequest
    {
        [JsonPropertyName("from_district_id")]
        public int FromDistrictId { get; set; }

        [JsonPropertyName("from_ward_code")]
        public string FromWardCode { get; set; } = "";

        [JsonPropertyName("service_type_id")]
        public int ServiceTypeId { get; set; }

        [JsonPropertyName("to_district_id")]
        public int ToDistrictId { get; set; }

        [JsonPropertyName("to_ward_code")]
        public string ToWardCode { get; set; } = "";

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("length")]
        public int Length { get; set; }

        [JsonPropertyName("weight")]
        public int Weight { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("insurance_value")]
        public int InsuranceValue { get; set; }
    }

    private sealed class GHNFeeResponse
    {
        [JsonPropertyName("data")]
        public GHNFeeData? Data { get; set; }
    }

    private sealed class GHNFeeData
    {
        [JsonPropertyName("total")]
        public decimal Total { get; set; }
    }

    private sealed class GHNCreateOrderRequest
    {
        [JsonPropertyName("payment_type_id")]
        public int PaymentTypeId { get; set; }

        [JsonPropertyName("note")]
        public string? Note { get; set; }

        [JsonPropertyName("required_note")]
        public string RequiredNote { get; set; } = "";

        [JsonPropertyName("from_name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? FromName { get; set; }

        [JsonPropertyName("from_phone")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? FromPhone { get; set; }

        [JsonPropertyName("from_address")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? FromAddress { get; set; }

        [JsonPropertyName("from_ward_name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? FromWardName { get; set; }

        [JsonPropertyName("from_district_name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? FromDistrictName { get; set; }

        [JsonPropertyName("from_province_name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? FromProvinceName { get; set; }

        [JsonPropertyName("return_phone")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ReturnPhone { get; set; }

        [JsonPropertyName("return_address")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ReturnAddress { get; set; }

        [JsonPropertyName("return_district_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? ReturnDistrictId { get; set; }

        [JsonPropertyName("return_ward_code")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ReturnWardCode { get; set; }

        [JsonPropertyName("client_order_code")]
        public string ClientOrderCode { get; set; } = "";

        [JsonPropertyName("to_name")]
        public string ToName { get; set; } = "";

        [JsonPropertyName("to_phone")]
        public string ToPhone { get; set; } = "";

        [JsonPropertyName("to_address")]
        public string ToAddress { get; set; } = "";

        [JsonPropertyName("to_ward_code")]
        public string ToWardCode { get; set; } = "";

        [JsonPropertyName("to_district_id")]
        public int ToDistrictId { get; set; }

        [JsonPropertyName("cod_amount")]
        public int CodAmount { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; } = "";

        [JsonPropertyName("weight")]
        public int Weight { get; set; }

        [JsonPropertyName("length")]
        public int Length { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("insurance_value")]
        public int InsuranceValue { get; set; }

        [JsonPropertyName("service_type_id")]
        public int ServiceTypeId { get; set; }

        [JsonPropertyName("pick_shift")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int[]? PickShift { get; set; }

        [JsonPropertyName("items")]
        public List<GHNCreateOrderItemRequest> Items { get; set; } = new();
    }

    private sealed class GHNCreateOrderItemRequest
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("code")]
        public string Code { get; set; } = "";

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [JsonPropertyName("price")]
        public int Price { get; set; }

        [JsonPropertyName("length")]
        public int Length { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("weight")]
        public int Weight { get; set; }
    }

    private sealed class GHNCreateOrderResponse
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("data")]
        public GHNCreateOrderData? Data { get; set; }
    }

    private sealed class GHNCreateOrderData
    {
        [JsonPropertyName("order_code")]
        public string? OrderCode { get; set; }

        [JsonPropertyName("total_fee")]
        public decimal? TotalFee { get; set; }
    }

    private sealed class GHNProvinceResponse
    {
        [JsonPropertyName("data")]
        public List<GHNProvince>? Data { get; set; }
    }

    private sealed class GHNProvince
    {
        [JsonPropertyName("ProvinceID")]
        public int ProvinceId { get; set; }

        [JsonPropertyName("ProvinceName")]
        public string? ProvinceName { get; set; }
    }

    private sealed class GHNDistrictResponse
    {
        [JsonPropertyName("data")]
        public List<GHNDistrict>? Data { get; set; }
    }

    private sealed class GHNDistrict
    {
        [JsonPropertyName("DistrictID")]
        public int DistrictId { get; set; }

        [JsonPropertyName("DistrictName")]
        public string? DistrictName { get; set; }
    }

    private sealed class GHNWardResponse
    {
        [JsonPropertyName("data")]
        public List<GHNWard>? Data { get; set; }
    }

    private sealed class GHNWard
    {
        [JsonPropertyName("WardCode")]
        public string? WardCode { get; set; }

        [JsonPropertyName("WardName")]
        public string? WardName { get; set; }
    }
}
