namespace EXE.Services;

public class GHNSettings
{
    public string Token { get; set; } = "";
    public int ShopId { get; set; }
    public string FeeUrl { get; set; } = "https://dev-online-gateway.ghn.vn/shiip/public-api/v2/shipping-order/fee";
    public string CreateOrderUrl { get; set; } = "https://dev-online-gateway.ghn.vn/shiip/public-api/v2/shipping-order/create";
    public string ProvinceUrl { get; set; } = "https://dev-online-gateway.ghn.vn/shiip/public-api/master-data/province";
    public string DistrictUrl { get; set; } = "https://dev-online-gateway.ghn.vn/shiip/public-api/master-data/district";
    public string WardUrl { get; set; } = "https://dev-online-gateway.ghn.vn/shiip/public-api/master-data/ward";
    public int FromDistrictId { get; set; }
    public string FromWardCode { get; set; } = "";
    public string FromName { get; set; } = "";
    public string FromPhone { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string FromWardName { get; set; } = "";
    public string FromDistrictName { get; set; } = "";
    public string FromProvinceName { get; set; } = "";
    public int ServiceTypeId { get; set; } = 2;
    public int PaymentTypeId { get; set; } = 2;
    public string RequiredNote { get; set; } = "KHONGCHOXEMHANG";
    public int[] PickupShiftIds { get; set; } = Array.Empty<int>();
    public int DefaultWeight { get; set; } = 1000;
    public int DefaultLength { get; set; } = 20;
    public int DefaultWidth { get; set; } = 15;
    public int DefaultHeight { get; set; } = 10;
    public decimal FallbackFee { get; set; } = 25000m;
}
