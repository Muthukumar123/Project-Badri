using System.Xml.Linq;
namespace Badri.Pilot;
public static class Seed
{
    // Historical EXCERPTS from the seven uploads (reviewed in v2.0).
    // These are reference-only; none are posted as dues/stock/cash on first login.
    public static void Load(XDocument d)
    {
        var r=d.Root!;
        foreach(var (name,beat) in new[]{
            ("Kumar Maligai","Bommidi"),("Kuriji Store","Bommidi"),("Siva Sakthi Maligai","Bommidi"),
            ("Raja (F)","Bommidi"),("Velmurugan Maligai","Bommidi"),("Sakthi Maligai","Bommidi"),
            ("KSS Maligai","Bommidi"),("Angalamman S/M","GK Road"),("Santhosh Store","GK Road"),
            ("Kanagavalli Store","GK Road"),("Sri Lakshmi S/M","GK Road"),("Ganga (F)","GK Road"),
            ("Naveen S/M","GK Road"),("Saravana Store","GK Road")})
            r.Element("retailers")!.Add(PilotStore.Obj("retailer",("id",PilotStore.Id()),("name",name),("beat",beat),
                ("status","Imported for review"),("openingDue","0")));
        foreach(var supplier in new[]{"PA Traders","Maha Enterprises","Himalaya Wellness"})
            r.Element("suppliers")!.Add(PilotStore.Obj("supplier",("id",PilotStore.Id()),("name",supplier)));
        foreach(var p in new[]{
            ("KOPIKO CAPPU 12X240JAR", "PA Traders", "0"),("CHOKI CHOKI 12J", "PA Traders", "0"),
            ("CHOKI STIX 12GBX", "PA Traders", "0"),("CHOKI CHOKI XL PACK", "PA Traders", "0"),
            ("KOPIKO CAPPU 48 PC", "PA Traders", "0"),("KOPIKO CAPPU 32 POUCH", "PA Traders", "0"),
            ("MALKIEST CHEESE FAMILY", "PA Traders", "0"),("KOP MALKIEST DC FAMILKY", "PA Traders", "0"),
            ("MALKIEST CAPPUCCINO", "PA Traders", "0"),("MALKIEST CHEESE FREE", "PA Traders", "0"),
            ("HARPIC 450ML","Maha Enterprises","0"),("DET ASL 60ML","Maha Enterprises","0"),
            ("DET ASL 125ML","Maha Enterprises","0"),("LYSOL DFC FLORAL 500ML","Maha Enterprises","0"),
            ("GENTLE BABY SOAP 125G","Himalaya Wellness","0")})
            r.Element("products")!.Add(PilotStore.Obj("product",("id",PilotStore.Id()),("name",p.Item1),
                ("supplier",p.Item2),("initialStock",p.Item3),("safetyStock","20"),("dailyDemand","0"),
                ("leadDays","4"),("pack","1")));
        foreach(var sample in new[]{
            ("Collection sheet", "Vijay - Bommidi, 12/1?/2026 (date unclear)", "34178", "34186", "34172", "11 rows; handwritten amount for Velmurugan uncertain (230 or 238); 6 rupee cash difference only if scope confirmed"),
            ("Collection sheet", "R. Santhosh - GK Road, 14 Nov 2026", "112774", "112754", "88586", "Credit lines differ by 20; cash denominations 88586; payment modes missing; HC-3000 / HC-1000"),
            ("Delivery sheet", "Santhosh - GK Road, 14 Nov 2026", "166040", "166041", "", "46 rows; 44 valued; paper marks and 17 uncertain entries; not proof of delivery"),
            ("Supplier invoice", "PA Traders A000770", "57977", "57977", "", "Credit bill; 10 lines, 27 paid units and 60 promotional free units"),
            ("Supplier invoice", "Maha Enterprises ME0398", "26059", "26059", "", "IRN present; 5% and 18% GST; four lines"),
            ("Supplier invoice", "Maha Enterprises MHHIN009140", "", "", "", "Page 1 only; 18 product lines; invoice total missing; no approval"),
            ("Supplier invoice", "Himalaya Wellness 5066110913", "", "", "", "Page 1 of 2; second page needed; expiry and free offers")})
            r.Element("documents")!.Add(PilotStore.Obj("document",("id",PilotStore.Id()),("type",sample.Item1),
                ("title",sample.Item2),("claimed",sample.Item3),("computed",sample.Item4),("cashCount",sample.Item5),
                ("notes",sample.Item6),("status","Review required"),("source","Original customer scan not stored in Git")));
        // Detailed, review-only transcribed paper rows. Neither sales nor receipts
        // are posted from this table. Uncertain handwriting is explicitly flagged.
        var documentGroup=r.Element("documents")!;
        var gk=documentGroup.Elements().First(x=>PilotStore.Attr(x,"title").StartsWith("Santhosh - GK Road"));
        var gkRows=new (string shop,string bill,string mark)[]{
          ("Shri Dhanush Dept","4811","tick"),("Sathya Maligai","1486","tick"),("SRM Maligai","3355","G/P circle"),
          ("Sri Vinayaga Maligai","200","tick"),("Kumar Maligai (Vasi)","1548","tick"),("RK Medical","1470","circle"),
          ("Dhanshika Maligai","1760","circle; collected sheet 1750"),("Idhu Namma Kadai","2464","tick"),
          ("Angalamman Maligai","1139","G/P circle"),("Saravana Store","","old bill #5993"),
          ("Angalamman S/M","6679","HC-3000 circle"),("Local customer","548","tick"),
          ("OM Muruga (F)","3630","G/P circle"),("Alagumayil (F)","616","tick"),
          ("Preethvi Maligai","227","uncertain tick"),("Selvam Maligai","769","uncertain tick"),
          ("Baskar Maligai","394","tick"),("Santhosh Store","7792","G/P overwritten"),
          ("Livoia (F)","690","circle"),("Sri Krishna S/M","4268","uncertain G/P"),
          ("Malar (F)","591","tick"),("Syed Peer Maligai","914","tick"),
          ("Vinayaga Dept (Than)","4385","tick"),("Kovai Lala","1274","HC-1000 circle"),
          ("Shanmuga (F)","3427","tick"),("Naveen S/M","7962","circle"),
          ("Esu Dept","4748","tick"),("Ganga (F)","4861","circle; collection 4867"),
          ("Kavi Pharmacy","2635","circle"),("Amman (F) (OKR)","","struck out"),
          ("Sri Lakshmi S/M","35227","uncertain circle"),("Iyyappa (F)","1152","tick"),
          ("Elango Maligai","2562","G/P circle"),("Jeevan S/M","1430","circle"),
          ("Kanagavalli Store","3024","circle; collection 3004"),("Monesh S/M","2431","uncertain tick"),
          ("Mini Mart","3179","tick"),("Janani Department","3114","tick"),
          ("Sree Nee Super Market","2316","circle"),("Aara Super Market","4505","circle"),
          ("Vinayaga Department","18113","uncertain circle"),("Balasi Dept","10294","tick"),
          ("Lakshmi Maligai","134","circle"),("SP Muthu (F)","1952","circle"),
          ("AKS Maligai","1337","tick"),("Priyan S/M","628","tick")
        };
        foreach(var (shop,bill,mark) in gkRows)gk.Add(PilotStore.Obj("row",("shop",shop),("amount",bill),("mark",mark)));
        var vijay=documentGroup.Elements().First(x=>PilotStore.Attr(x,"title").StartsWith("Vijay"));
        var vijayRows=new (string bill,string shop,string amount)[]{
          ("6296","Kumar Maligai","1376"),("6163","Kuriji Store","2168"),("5730","Kuriji Store","948"),
          ("3444","Siva Sakthi Maligai","5000"),("6298","Sakthi (F)","3184"),
          ("6112","Raja (F)","11847"),("6168","Velmurugan Maligai","238"),
          ("6175","Sakthi Maligai","5089"),("6113","Sadosa (F)","2079"),
          ("6096","ARK Store","1131"),("6319","KSS Maligai","1126")};
        foreach(var(bill,shop,amount)in vijayRows)
          vijay.Add(PilotStore.Obj("row",("bill",bill),("shop",shop),("amount",amount),
              ("mark",bill=="6168"?"Possible correction 230 / written subtotal differs":"Unverified paper")));
        var santhosh=documentGroup.Elements().First(x=>PilotStore.Attr(x,"title").StartsWith("R. Santhosh"));
        var santhoshRows=new (string shop,string amount,string detail)[]{
          ("Angalamman S/M","3679","HC-3000 on delivery"),("Livoia (F)","690",""),("Naveen S/M","7962",""),
          ("Ganga (F)","4867","delivery 4861"),("Kavi Pharmacy","2635",""),("Lakshmi S/M","35227",""),
          ("Jeevan S/M","1430",""),("Kanagavalli Store","3004","possible 3024"),
          ("Vinayaga Dept","18113",""),("Lakshmi Maligai","134",""),("SP Muthu","1952",""),
          ("RK Medical","1470",""),("Dhanshika Maligai","1750","delivery 1760"),
          ("Elango Maligai","2562",""),("OM Muruga (F)","3630",""),("Kovai Lala","274","HC-1000 on delivery"),
          ("Sri Krishna Dept","4268",""),("Santhosh Store","7792",""),("Angalamman Maligai","1139",""),
          ("SRM Maligai","3355",""),("Sree Nee S/M","2316",""),("Aara S/M","4505","")};
        foreach(var(shop,amount,detail) in santhoshRows)
          santhosh.Add(PilotStore.Obj("row",("shop",shop),("amount",amount),("mark",detail),("type","Credit sheet")));
        var pa=documentGroup.Elements().First(x=>PilotStore.Attr(x,"title").StartsWith("PA Traders"));
        foreach(var (product,qty,amount) in new[]{
          ("KOPIKO CAPPU 12X240JAR","10","22695.20"),("CHOKI CHOKI 12J","6","13245.12"),
          ("CHOKI STIX 12GBX","2","3594.00"),("CHOKI CHOKI XL PACK","1","2000.00"),
          ("KOPIKO CAPPU 48 PC","1","1815.40"),("KOPIKO CAPPU 32 POUCH","1","2572.08"),
          ("MALKIEST CHEESE FAMILY","3","6127.38"),("KOP MALKIEST DC FAMILKY","2","4084.92"),
          ("MALKIEST CAPPUCCINO","1","2042.46"),("MALKIEST CHEESE FREE","60 FREE","0.00")})
          pa.Add(PilotStore.Obj("row",("shop",product),("amount",amount),("mark",qty)));
        var me=documentGroup.Elements().First(x=>PilotStore.Attr(x,"title").Contains("ME0398"));
        foreach(var(product,qty,amount)in new[]{
          ("DET ASL 60ML","200","7696.50"),("DET ASL 125ML","96","7394.68"),
          ("HARPIC 450ML","120","9008.93"),("LYSOL FLORAL 500ML","18","1958.66")})
          me.Add(PilotStore.Obj("row",("shop",product),("amount",amount),("mark",qty+" units")));
        foreach(var bill in new[]{
            ("PA Traders","A000770","57977","10","Complete"),
            ("Maha Enterprises","ME0398","26059","4","Complete"),
            ("Maha Enterprises","MHHIN009140","","18","Missing page"),
            ("Himalaya Wellness","5066110913","","9","Missing page")})
            r.Element("purchases")!.Add(PilotStore.Obj("purchase",("id",PilotStore.Id()),("supplier",bill.Item1),
                ("invoice",bill.Item2),("amount",bill.Item3),("lines",bill.Item4),("completeness",bill.Item5),
                ("status","Review required"),("received","0"),("paid","0")));
        var pInvoices=r.Element("purchases")!;
        var paInvoice=pInvoices.Elements().First(x=>PilotStore.Attr(x,"invoice")=="A000770");
        var paExpected=new (string sku,string qty)[]{
          ("KOPIKO CAPPU 12X240JAR","10"),("CHOKI CHOKI 12J","6"),
          ("CHOKI STIX 12GBX","2"),("CHOKI CHOKI XL PACK","1"),
          ("KOPIKO CAPPU 48 PC","1"),("KOPIKO CAPPU 32 POUCH","1"),
          ("MALKIEST CHEESE FAMILY","3"),("KOP MALKIEST DC FAMILKY","2"),
          ("MALKIEST CAPPUCCINO","1"),("MALKIEST CHEESE FREE","60")};
        foreach(var(sku,qty) in paExpected)paInvoice.Add(PilotStore.Obj("expected",("sku",sku),("qty",qty)));
        var meInvoice=pInvoices.Elements().First(x=>PilotStore.Attr(x,"invoice")=="ME0398");
        foreach(var(sku,qty) in new[]{("DET ASL 60ML","200"),("DET ASL 125ML","96"),
                ("HARPIC 450ML","120"),("LYSOL DFC FLORAL 500ML","18")})
            meInvoice.Add(PilotStore.Obj("expected",("sku",sku),("qty",qty)));
    }
}
