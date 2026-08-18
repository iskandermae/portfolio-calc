using PortfolioCalc.Core.Domain;

namespace PortfolioCalc.Core.Tests.Core.Domain;

public class TransactionCategoryClassifierTests
{
    [Theory]
    [InlineData(CashTransactionType.Deposit, TransactionCategory.Primary)]
    [InlineData(CashTransactionType.Withdrawal, TransactionCategory.Primary)]
    [InlineData(CashTransactionType.Interest, TransactionCategory.Secondary)]
    public void Classify_CashTransactionType_matches_the_expected_category(
        CashTransactionType type, TransactionCategory expected)
    {
        Assert.Equal(expected, TransactionCategoryClassifier.Classify(type));
    }

    [Theory]
    [InlineData(SecurityTransactionType.Buy, TransactionCategory.Primary)]
    [InlineData(SecurityTransactionType.Sell, TransactionCategory.Primary)]
    [InlineData(SecurityTransactionType.TransferIn, TransactionCategory.Primary)]
    [InlineData(SecurityTransactionType.Dividend, TransactionCategory.Secondary)]
    [InlineData(SecurityTransactionType.Tax, TransactionCategory.Secondary)]
    public void Classify_SecurityTransactionType_matches_the_expected_category(
        SecurityTransactionType type, TransactionCategory expected)
    {
        Assert.Equal(expected, TransactionCategoryClassifier.Classify(type));
    }

    [Fact]
    public void Classify_covers_every_CashTransactionType_value()
    {
        foreach (var type in Enum.GetValues<CashTransactionType>())
            TransactionCategoryClassifier.Classify(type); // throws if unclassified
    }

    [Fact]
    public void Classify_covers_every_SecurityTransactionType_value()
    {
        foreach (var type in Enum.GetValues<SecurityTransactionType>())
            TransactionCategoryClassifier.Classify(type); // throws if unclassified
    }
}
