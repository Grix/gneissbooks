using GneissBooks;
using GneissBooks.Saft;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Configuration;

namespace GneissBookTests;

[TestClass]
public class MainTests
{
    public MainTests()
    {
        App.UserData = new ConfigurationBuilder().AddUserSecrets<App>().Build();
    }

    [TestMethod]
    public void SaveLoadTest()
    {
        var books = new SaftBooks();
        books.Load(Path.Combine("TestFiles", "ExampleFile SAF-T Financial_888888888_20180228235959.xml"));
        var temp = Path.GetTempFileName();
        books.Save(temp);
        books.Load(temp);
        books.Save(temp);
        books.Load(Path.Combine("TestFiles", "ExampleFile SAF-T Financial_999999999_20161125213512.xml"));
        books.Save(temp + "2");
        books.Load(temp + "2");
        books.Save(temp);
    }

    [TestMethod]
    public void RecalculateBalancesTest()
    {
        var books = new SaftBooks();
        books.Load(Path.Combine("TestFiles", "ExampleFile SAF-T Financial_888888888_20180228235959.xml"));
        books.RecalculateBalances();

        var account1 = books.Accounts.Find(_account => _account.AccountID == "1250")!;
        Assert.AreEqual(132500m, account1.Item, 0);
        Assert.IsTrue(account1.ItemElementName == GneissBooks.Saft.ItemChoiceType.OpeningDebitBalance);
        Assert.AreEqual(145500m, account1.Item1, 0);
        Assert.IsTrue(account1.Item1ElementName == GneissBooks.Saft.Item1ChoiceType.ClosingDebitBalance);

        var account2 = books.Accounts.Find(_account => _account.AccountID == "1420")!;
        Assert.AreEqual(957000m, account2.Item, 0);
        Assert.IsTrue(account2.ItemElementName == GneissBooks.Saft.ItemChoiceType.OpeningDebitBalance);
        Assert.AreEqual(957000m, account2.Item1, 0);
        Assert.IsTrue(account2.Item1ElementName == GneissBooks.Saft.Item1ChoiceType.ClosingDebitBalance);

        var account3 = books.Accounts.Find(_account => _account.AccountID == "1500")!;
        Assert.AreEqual(15000m, account3.Item, 0);
        Assert.IsTrue(account3.ItemElementName == GneissBooks.Saft.ItemChoiceType.OpeningDebitBalance);
        Assert.AreEqual(103700m, account3.Item1, 0);
        Assert.IsTrue(account3.Item1ElementName == GneissBooks.Saft.Item1ChoiceType.ClosingDebitBalance);

        var account4 = books.Accounts.Find(_account => _account.AccountID == "2700")!;
        Assert.AreEqual(300000m, account4.Item, 0);
        Assert.IsTrue(account4.ItemElementName == GneissBooks.Saft.ItemChoiceType.OpeningCreditBalance);
        Assert.AreEqual(326375m, account4.Item1, 0);
        Assert.IsTrue(account4.Item1ElementName == GneissBooks.Saft.Item1ChoiceType.ClosingCreditBalance);

        var account5 = books.Accounts.Find(_account => _account.AccountID == "2711")!;
        Assert.AreEqual(0m, account5.Item, 0);
        Assert.AreEqual(0m, account5.Item1, 0.5m); // TODO why the discrepancy? Rounding accepted because Skatteetaten doesn't allow decimals on VAT settlements?
    }

    [TestMethod]
    public void ForeignCurrencyTest()
    {
        var books = new SaftBooks();

        books.AddTransaction(new DateTime(2024, 01, 15), "Test100USDSale", new List<TransactionLine>()
        {
            new TransactionLine(100m, "1501", currency: "USD"),
            new TransactionLine(-100, "3000", currency: "USD"),
        }).Wait();
        // Exchange rate on 2024-01-15 should be 10.359744259497308018751136315

        Assert.AreEqual("1501", books.Transactions.First().Line.First().AccountID);
        Assert.AreEqual("3000", books.Transactions.First().Line.Last().AccountID);

        Assert.AreEqual(100m * 10.359744259497308018751136315m, books.Transactions.First().Line.First().Item.Amount, 1m);
        Assert.AreEqual(100m, books.Transactions.First().Line.First().Item.CurrencyAmount, 0m);
        Assert.AreEqual(10.359744259497308018751136315m, books.Transactions.First().Line.First().Item.ExchangeRate, 0.01m);
        Assert.AreEqual(ItemChoiceType4.DebitAmount, books.Transactions.First().Line.First().ItemElementName);

        Assert.AreEqual(100m * 10.359744259497308018751136315m, books.Transactions.Last().Line.Last().Item.Amount, 1m);
        Assert.AreEqual(100m, books.Transactions.First().Line.Last().Item.CurrencyAmount, 0m);
        Assert.AreEqual(10.359744259497308018751136315m, books.Transactions.First().Line.Last().Item.ExchangeRate, 0.01m);
        Assert.AreEqual(ItemChoiceType4.CreditAmount, books.Transactions.First().Line.Last().ItemElementName);

        var temp = Path.GetTempFileName();
        books.Save(temp);
        books.Load(temp);

        Assert.AreEqual("1501", books.Transactions.First().Line.First().AccountID);
        Assert.AreEqual("3000", books.Transactions.First().Line.Last().AccountID);

        Assert.AreEqual(100m * 10.359744259497308018751136315m, books.Transactions.First().Line.First().Item.Amount, 1m);
        Assert.AreEqual(100m, books.Transactions.First().Line.First().Item.CurrencyAmount, 0m);
        Assert.AreEqual(10.359744259497308018751136315m, books.Transactions.First().Line.First().Item.ExchangeRate, 0.01m);
        Assert.AreEqual(ItemChoiceType4.DebitAmount, books.Transactions.First().Line.First().ItemElementName);

        Assert.AreEqual(100m * 10.359744259497308018751136315m, books.Transactions.Last().Line.Last().Item.Amount, 1m);
        Assert.AreEqual(100m, books.Transactions.First().Line.Last().Item.CurrencyAmount, 0m);
        Assert.AreEqual(10.359744259497308018751136315m, books.Transactions.First().Line.Last().Item.ExchangeRate, 0.01m);
        Assert.AreEqual(ItemChoiceType4.CreditAmount, books.Transactions.First().Line.Last().ItemElementName);

    }
}