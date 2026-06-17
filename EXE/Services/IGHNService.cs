namespace EXE.Services;

public interface IGHNService
{
    Task<decimal> CalculateFeeAsync(decimal insuranceValue, int toDistrictId, string toWardCode, int itemCount);
    Task<GHNCreateOrderResult> CreateOrderAsync(GHNCreateOrderInput input);
    Task<List<GHNLocationItem>> GetProvincesAsync();
    Task<List<GHNLocationItem>> GetDistrictsAsync(int provinceId);
    Task<List<GHNLocationItem>> GetWardsAsync(int districtId);
}

public class GHNCreateOrderInput
{
    public string ClientOrderCode { get; set; } = "";
    public string ToName { get; set; } = "";
    public string ToPhone { get; set; } = "";
    public string ToAddress { get; set; } = "";
    public int ToDistrictId { get; set; }
    public string ToWardCode { get; set; } = "";
    public string Note { get; set; } = "";
    public decimal InsuranceValue { get; set; }
    public IEnumerable<GHNCreateOrderItem> Items { get; set; } = Enumerable.Empty<GHNCreateOrderItem>();
}

public class GHNCreateOrderItem
{
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

public class GHNCreateOrderResult
{
    public bool Success { get; set; }
    public string? OrderCode { get; set; }
    public string? Message { get; set; }
    public decimal? TotalFee { get; set; }
}
