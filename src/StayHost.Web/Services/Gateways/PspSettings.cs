namespace StayHost.Web.Services.Gateways;

/// <summary>
/// docs/07 §13 phương án A — which licensed gateway takes the money, and with
/// whose keys.
///
/// Every one of these starts empty, and an empty one means the gateway is not
/// wired: the stand-in <see cref="StayHost.Web.Services.PaymentGateway"/> keeps
/// the checkout working exactly as it did. That is the same rule the bank
/// transfer follows — a payment method with no credentials behind it is not
/// offered rather than offered and broken.
///
/// Secrets belong in environment variables (<c>Psp__Vnpay__HashSecret</c>), not
/// in appsettings.json. docs/07 §14.4.
/// </summary>
public class PspSettings
{
    /// <summary>
    /// Where the gateway sends the guest back to, and where it posts its IPN.
    /// It has to be an address the gateway can reach, so on a laptop it is the
    /// local one for the return trip and the IPN simply never arrives — which is
    /// exactly why docs/07 §5 insists the platform ask the gateway itself.
    /// </summary>
    public string PublicUrl { get; set; } = "";

    public VnPayOptions Vnpay { get; set; } = new();
    public MoMoOptions Momo { get; set; } = new();
    public ZaloPayOptions Zalopay { get; set; } = new();

    /// <summary>
    /// Which gateway serves which of the methods on the checkout. Left empty a
    /// method falls back to the stand-in, so the four rows of docs/07 §2.1 can be
    /// switched on one at a time.
    /// </summary>
    public Dictionary<string, string> Methods { get; set; } = new()
    {
        ["card"] = Domain.Psp.VnPay,
        ["napas"] = Domain.Psp.VnPay,
        ["momo"] = Domain.Psp.MoMo,
        ["zalopay"] = Domain.Psp.ZaloPay
    };

    public class VnPayOptions
    {
        public string TmnCode { get; set; } = "";
        public string HashSecret { get; set; } = "";
        public string PayUrl { get; set; } = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
        public string ApiUrl { get; set; } = "https://sandbox.vnpayment.vn/merchant_webapi/api/transaction";
        public bool IsConfigured => TmnCode.Length > 0 && HashSecret.Length > 0;
    }

    public class MoMoOptions
    {
        public string PartnerCode { get; set; } = "";
        public string AccessKey { get; set; } = "";
        public string SecretKey { get; set; } = "";
        public string Endpoint { get; set; } = "https://test-payment.momo.vn/v2/gateway/api";
        public bool IsConfigured => PartnerCode.Length > 0 && AccessKey.Length > 0 && SecretKey.Length > 0;
    }

    public class ZaloPayOptions
    {
        public string AppId { get; set; } = "";
        public string Key1 { get; set; } = "";
        public string Key2 { get; set; } = "";
        public string Endpoint { get; set; } = "https://sb-openapi.zalopay.vn/v2";
        public bool IsConfigured => AppId.Length > 0 && Key1.Length > 0 && Key2.Length > 0;
    }
}
