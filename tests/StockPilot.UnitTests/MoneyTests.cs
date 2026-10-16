using StockPilot.Core;
namespace StockPilot.UnitTests;
public sealed class MoneyTests
{
    [Fact] public void TotalsRoundTaxAndApplyDiscountBeforeTax() { var t = Money.Totals([10.005m, 20m], 5m, 7.5m); Assert.Equal(30.01m, t.Subtotal); Assert.Equal(1.88m, t.Tax); Assert.Equal(26.89m, t.Total); }
    [Theory] [InlineData(-1)] [InlineData(0)] [InlineData(1000001)] [InlineData(1.0001)] public void InvalidQuantitiesFail(decimal n) => Assert.Throws<BusinessException>(() => Money.Quantity(n));
    [Theory] [InlineData(-1)] [InlineData(1000000000)] [InlineData(1.001)] public void InvalidPricesFail(decimal n) => Assert.Throws<BusinessException>(() => Money.Amount(n, "Price"));
    [Fact] public void DiscountCannotExceedSubtotal() => Assert.Throws<BusinessException>(() => Money.Totals([1], 2, 0));
    [Theory] [InlineData(-1)] [InlineData(101)] public void InvalidTaxFails(decimal rate) => Assert.Throws<BusinessException>(() => Money.Totals([1], 0, rate));
    [Fact] public void ZeroValueInvoiceIsPaidWithoutCreatingAPayment() => Assert.Equal("Paid", Money.PaymentState(0,0,0,false));
    [Fact] public void WeightedAverageUsesExistingAndReceivedStock() => Assert.Equal(4m, Money.Average(10, 2, 10, 6));
    [Fact] public void SaleStaffCannotAdjustInventory() => Assert.Throws<BusinessException>(() => new Actor("a", [Roles.Sales]).Demand(Roles.Inventory));
    [Fact] public void AdminCanManageEverything() => new Actor("a", [Roles.Admin]).Demand(Roles.Manager);
    [Fact] public void RefundDueAndPaymentStatesAreDerived() { Assert.Equal("RefundDue", Money.PaymentState(10, 8, 2, true)); Assert.Equal("PartiallyPaid", Money.PaymentState(10, 8, 0, false)); }
}
