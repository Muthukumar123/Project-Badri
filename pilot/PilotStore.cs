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
            doc = XDocument.Parse("<badri version='1'><users/><retailers/><suppliers/><products/><documents/><purchases/><stock/><bills/><deliveries/><collections/><cashcloses/><payments/><returns/><audit/></badri>");
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
    void Bootstrap(IConfiguration config)
    {
        var accounts = new (string user, string role, string key)[]
        {
            ("owner.badri","Owner","BADRI_PASS_OWNER"),
            ("accounts.badri","Accounts","BADRI_PASS_ACCOUNTS"),
            ("warehouse.badri","Warehouse","BADRI_PASS_WAREHOUSE"),
            ("salesman.santhosh","Salesman","BADRI_PASS_SALESMAN"),
            ("retailer.demo","Retailer","BADRI_PASS_RETAILER"),
            ("operator.demo","Operator","BADRI_PASS_OPERATOR")
        };
        var users = Group("users");
        var changed = false;
        foreach (var (username,role,key) in accounts)
        {
            if (users.Elements("user").Any(x=>Attr(x,"name")==username)) continue;
            var password = config[key];
            if (string.IsNullOrWhiteSpace(password) || password.Length < 14)
                throw new InvalidOperationException($"Set {key} to a unique 14+ character secret before first launch. Never commit credentials to Git.");
            users.Add(new XElement("user", new XAttribute("name",username),new XAttribute("role",role),
                new XAttribute("tenant",DemoTenant),new XAttribute("hash",HashPassword(password))));
            changed = true;
        }
        if (changed) Save();
    }
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
