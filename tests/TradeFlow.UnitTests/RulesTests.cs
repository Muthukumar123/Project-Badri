using Badri.TradeFlow;
using Xunit;

namespace TradeFlow.UnitTests;
public class RulesTests
{
    [Theory]
    [InlineData("0",false)]
    [InlineData("-4",false)]
    [InlineData("50.001",false)]
    [InlineData("50.00",true)]
    public void Amount_requires_paise_precision(string s,bool result)
      =>Assert.Equal(result,Rules.ValidMoney(decimal.Parse(s,System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void Upi_is_pending_not_cleared()
    {
       Assert.Equal(PaymentState.BankAwaitingVerification,Rules.InitialPaymentState(PaymentMode.Upi));
       Assert.False(Rules.ReducesDue(PaymentState.BankAwaitingVerification));
       Assert.True(Rules.ReducesDue(PaymentState.Cleared));
    }

    [Fact]
    public void Notes_from_vijay_sheet_have_exact_total()
    {
       var notes=new Dictionary<int,int>{{500,39},{200,37},{100,69},{50,6},{20,2},{10,1},{1,22}};
       Assert.Equal(34172m,Rules.CashCount(notes));
    }

    [Fact]
    public void No_receipt_fingerprint_reused_for_different_amount()
    {
       var id=Guid.NewGuid();
       Assert.NotEqual(Rules.Fingerprint(id,null,"6317",500m,PaymentMode.Cash),Rules.Fingerprint(id,null,"6317",501m,PaymentMode.Cash));
    }
}
