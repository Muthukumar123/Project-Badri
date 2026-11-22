using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Badri.Pilot;

// SINGLE-INSTANCE TEMPORARY STORE FOR REDACTED DEMO DATA ONLY.
// Do not use as a multi-replica accounting database. A live production build uses SQL.
public sealed class PilotStore
{
    readonly SemaphoreSlim gate = new(1, 1);
    readonly string file;
    XDocument doc;
    public const string DemoTenant = "badri-dharmapuri-pilot";
    public PilotStore(IConfiguration config, IWebHostEnvironment environment)
    {
        file = config["BADRI_XML_PATH"] ?? Path.Combine(environment.ContentRootPath, "pilot-data.xml");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        if (File.Exists(file)) doc = LoadSafe(file);
        else
        {
            doc = XDocument.Parse("<badri version='1'><users/><retailers/><suppliers/><products/><orders/><documents/><purchases/><stock/><bills/><deliveries/><collections/><cashcloses/><payments/><returns/><audit/></badri>");
            Seed.Load(doc);
            Save();
        }
        Bootstrap(config);
    }
    static XDocument LoadSafe(string path)
    {
        using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        return XDocument.Load(reader);
    }
    void Save()
    {
        var temp = file + ".tmp";
        doc.Save(temp, SaveOptions.None);
        File.Move(temp, file, true);
    }
    static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 310_000, HashAlgorithmName.SHA256, 32);
        return Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(hash);
    }
    public static bool Verify(string password, string? digest)
    {
        try
        {
            var parts = (digest ?? "").Split(':');
            if (parts.Length != 2) return false;
            var salt = Convert.FromBase64String(parts[0]);
            var expected = Convert.FromBase64String(parts[1]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, 310_000, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch { return false; }
    }
    // XML pilot migration. Preserve invoices, receipts, deliveries, stock and audit.
    // Only the login identities change to owner / salesperson / retailer.
    void Bootstrap(IConfiguration config)
    {
        var accounts = new (string user, string role, string primary, string fallback, string legacy)[]
        {
            ("owner", "Owner", "BADRI_PASS_OWNER", "", "owner.badri"),
            ("salesperson", "Salesperson", "BADRI_PASS_SALESPERSON", "BADRI_PASS_SALESMAN", "salesman.santhosh"),
            ("retailer", "Retailer", "BADRI_PASS_RETAILER", "", "retailer.demo")
        };
        var root = doc.Root ?? throw new InvalidOperationException("Pilot XML root missing");
        var users = Group("users");
        bool changed = false;
        if (root.Element("orders") is null) { root.Add(new XElement("orders")); changed = true; }

        // Preserve actor ownership on already entered pilot receipts and cash counts.
        foreach (var row in root.Descendants())
            foreach (var key in new[] { "by", "preparedBy", "approvedBy", "reviewedBy", "placedBy" })
            {
                var attribute = row.Attribute(key);
                if (attribute?.Value == "salesman.santhosh") { attribute.Value = "salesperson"; changed = true; }
                if (attribute?.Value == "owner.badri") { attribute.Value = "owner"; changed = true; }
            }

        foreach (var (username,role,primary,fallback,legacy) in accounts)
        {
            var password = config[primary] ?? (string.IsNullOrEmpty(fallback) ? null : config[fallback]);
            if (string.IsNullOrWhiteSpace(password) || password.Length < 14)
                throw new InvalidOperationException($"Configure {primary} (or legacy {fallback}) with a 14+ character secret in Azure App Settings.");

            var user = users.Elements("user")
                .FirstOrDefault(x=>Attr(x,"name")==username)
                ?? users.Elements("user").FirstOrDefault(x=>Attr(x,"name")==legacy);
            if (user is null)
            {
                user = new XElement("user"); users.Add(user); changed = true;
            }
            if (Attr(user,"name")!=username || Attr(user,"role")!=role || Attr(user,"tenant")!=DemoTenant)
            {
                user.SetAttributeValue("name", username);
                user.SetAttributeValue("role", role);
                user.SetAttributeValue("tenant", DemoTenant);
                changed = true;
            }
            // Explicit App Setting password rotation. Azure restarts the worker on
            // configuration changes. Existing secure-cookie sessions are invalidated.
            if (!Verify(password, Attr(user,"hash")))
            {
                user.SetAttributeValue("hash", HashPassword(password));
                changed = true;
            }
        }
        var active = new HashSet<string>(accounts.Select(x=>x.user),StringComparer.Ordinal);
        foreach (var obsolete in users.Elements("user").Where(x=>!active.Contains(Attr(x,"name"))).ToList())
        {
            obsolete.Remove(); changed = true;
        }
        if (changed)
        {
            if (File.Exists(file) && !File.Exists(file + ".before-three-roles.bak"))
                File.Copy(file,file + ".before-three-roles.bak");
            Save();
        }
    }

    // A login cookie is tied to the current password hash in persisted XML.
    // A portal password change invalidates every earlier session after app restart.
    public Task<string> SessionStamp(string username) => Read(d=>
    {
        var user=d.Root?.Element("users")?.Elements("user")
            .FirstOrDefault(x=>Attr(x,"name")==username);
        if(user is null)return "";
        var value=Attr(user,"hash");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    });
    public Task<string> RoleForUser(string username) => Read(d=>
        Attr(d.Root?.Element("users")?.Elements("user")
            .FirstOrDefault(x=>Attr(x,"name")==username),"role"));
    public XElement Group(string group) => doc.Root!.Element(group) ?? throw new InvalidOperationException("Missing XML collection: "+group);
    public static string Attr(XElement? node,string name) => node?.Attribute(name)?.Value ?? "";
    public static decimal Dec(XElement? node,string name) => decimal.TryParse(Attr(node,name),NumberStyles.Number,CultureInfo.InvariantCulture,out var v)?v:0m;
    public static string Money(decimal value) => value.ToString("0.00",CultureInfo.InvariantCulture);
    public static XElement? ById(XElement group,string id) => group.Elements().FirstOrDefault(x=>Attr(x,"id")==id);
    public static string Id() => Guid.NewGuid().ToString("N");
    public static XElement Obj(string name,params (string k, object? v)[] properties)
    {
        var item = new XElement(name);
        foreach (var (k,v) in properties) item.SetAttributeValue(k, v switch { decimal d => Money(d),DateTime dt => dt.ToUniversalTime().ToString("O"), _ => v?.ToString() ?? "" });
        return item;
    }
    public async Task<T> Read<T>(Func<XDocument,T> read)
    {
        await gate.WaitAsync();
        try { return read(doc); }
        finally { gate.Release(); }
    }
    public async Task<T> Write<T>(string actor,string action, Func<XDocument,T> write)
    {
        await gate.WaitAsync();
        try
        {
            var snapshot = new XDocument(doc);
            var result = write(snapshot);
            snapshot.Root!.Element("audit")!.Add(Obj("event",("id",Id()),("actor",actor),("action",action),("at",DateTime.UtcNow),("tenant",DemoTenant)));
            var old = doc; doc = snapshot;
            try { Save(); } catch { doc = old; throw; }
            return result;
        }
        finally { gate.Release(); }
    }
    public Task<(string role,bool ok)> CheckPassword(string user,string password) => Read(d => {
        var found=d.Root!.Element("users")!.Elements("user").FirstOrDefault(x => Attr(x,"name")==user);
        return (Attr(found,"role"),found is not null && Verify(password,Attr(found,"hash")));
    });
}
