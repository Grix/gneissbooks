using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rystem.OpenAi;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Transactions;

namespace GneissBooks.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _saftFileImportPath = "";
    [ObservableProperty]
    private string _saftFileExportPath = "";
    [ObservableProperty]
    private string[] _invoiceToProcessPaths = Array.Empty<string>();

    [ObservableProperty]
    private ObservableCollection<TransactionViewModel> _transactionList = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedTransaction))]
    [NotifyPropertyChangedFor(nameof(SelectedTransaction))]
    private int _selectedTransactionIndex = -1;

    public TransactionViewModel? SelectedTransaction => HasSelectedTransaction ? TransactionList[SelectedTransactionIndex] : null;

    [ObservableProperty]
    public TransactionViewModel _newManualTransaction = new();

    public bool HasSelectedTransaction => SelectedTransactionIndex >= 0 || Design.IsDesignMode;

    OpenAiApi openAi = new();
    Books books = new();

    decimal heliosProductionCost = 265.85m;
    decimal cableProductionCost = 51.49m;

    public MainViewModel()
    {
    }

    [RelayCommand]
    public async Task LoadSaftFile()
    {
        try
        {
            books.Load(SaftFileImportPath);
            await RefreshTransactionList();
        }
        catch { }
    }

    [RelayCommand]
    public async Task SaveSaftFile()
    {
        try
        {
            books.Save(SaftFileExportPath);
        }
        catch { }
    }

    [RelayCommand]
    public async Task ProcessSalesInvoices()
    {
        foreach (var invoicePath in InvoiceToProcessPaths)
            await ProcessSalesInvoice(invoicePath);
    }

    public async Task ProcessSalesInvoice(string path)
    {
        if (!File.Exists(path))
            throw new Exception("Invoice file doesn't exist");

        Console.WriteLine("Processing: " + path);

        var invoiceText = PdfReader.ExtractTextFromPdf(path);
        openAi.Initialize();
        var response = await openAi.ChatAndReceiveResponse(invoiceText, @"You are a bookkeeping robot parsing sales invoices for the company Mikkelsen Innovasjon. We sell the following products: Helios Laser DAC (SKU \""helios\"", ILDA cable (SKU \""db25\"", and LaserShowGen software (SKU \""lsg\""). You will be given pasted raw text from an invoice, and you are to respond with the following extracted information in json format: \""order_sum\"", \""invoice_date\"", \""payment_method\"", \""buyer_full_name\"", \""buyer_country\"",\""product_helios_quantity\"", \""product_db25_quantity\"", \""product_lsg_quantity\"",\""currency_code\"",\""order_number\"",\""ebay_user\"". All numerical fields should contain nothing but numbers. The payment method field should contain one of the following options: Stripe, Paypal, or Other. The invoice date should be in YYYY-MM-DD format. The invoice is either in USD or EUR. ebay_user can be empty if the order is not from Ebay.");
        JsonNode bilagData = JsonNode.Parse(response)!;
        var sumInForeignCurrency = decimal.Parse(bilagData["order_sum"]!.ToString());
        var isEbay = invoiceText.ToLower().Contains("ebay");
        var productionCost = int.Parse(bilagData["product_helios_quantity"]!.ToString()) * heliosProductionCost + int.Parse(bilagData["product_db25_quantity"]!.ToString()) * cableProductionCost;
        var currency = bilagData["currency_code"]!.ToString();
        var country = bilagData["buyer_country"]!.ToString().ToLower();
        var customerAccount = "1505";
        if (isEbay)
            customerAccount = "1503";
        else if (bilagData["payment_method"]!.ToString().ToLower().Contains("stripe"))
            customerAccount = "1502";
        else if (bilagData["payment_method"]!.ToString().ToLower().Contains("paypal"))
            customerAccount = "1501";

        var lines = new List<TransactionLine>();

        lines.Add(new TransactionLine(sumInForeignCurrency, customerAccount, "Kundefordring", currency));
        if (country == "no" || country == "norway")
            lines.Add(new TransactionLine(-sumInForeignCurrency, "3000", "Salgsinntekt", currency, TaxCodes.OutgoingSaleTaxHighRate));
        else
            lines.Add(new TransactionLine(-sumInForeignCurrency, "3100", "Salgsinntekt", currency, TaxCodes.Export));
        // todo tax codes etc
        if (productionCost > 0)
        {
            lines.Add(new TransactionLine(-productionCost, "1420", "Endring varelager"));
            lines.Add(new TransactionLine(productionCost, "4100", "Forbruk varer/deler"));
        }

        var documentId = await books.AddTransaction(DateOnly.ParseExact(bilagData["invoice_date"]!.ToString(), "yyyy-MM-dd").ToDateTime(default), "Salg", lines);

        NewManualTransaction = new();
        await RefreshTransactionList();
    }

    [RelayCommand]
    public async Task CopyNewTransactionFromSelection()
    {
        NewManualTransaction = new();
        if (SelectedTransaction == null)
            return;

        NewManualTransaction.Description = SelectedTransaction.Description;
        NewManualTransaction.Date = SelectedTransaction.Date;
        foreach (var line in SelectedTransaction.Lines)
        {
            var newLine = new TransactionLineViewModel
            {
                Currency = line.Currency,
                Amount = line.Amount,
                Description = line.Description,
                CustomerId = line.CustomerId,
                SupplierId = line.SupplierId,
                AccountId = line.AccountId,
                TaxCodeSelectionIndex = line.TaxCodeSelectionIndex,
            };
            NewManualTransaction.Lines.Add(newLine);
        }
    }

    [RelayCommand]
    public async Task AddNewManualTransaction()
    {
        var lines = new List<TransactionLine>();
        decimal totalAmount = 0;
        foreach (var line in NewManualTransaction.Lines)
        {
            if (!decimal.TryParse(line.Amount, out decimal amount))
                throw new Exception("Invalid amount in line: " + line.Amount);
            totalAmount += amount;
            lines.Add(new TransactionLine(amount, line.AccountId, line.Description, line.Currency, line.TaxCode, line.CustomerId, line.SupplierId));
        }
        if (totalAmount != 0)
            throw new Exception("Debit and credit amounts do not cancel out. Double check balance.");

        await books.AddTransaction(NewManualTransaction.Date, NewManualTransaction.Description, lines);

        NewManualTransaction = new();
        await RefreshTransactionList();
    }

    async Task RefreshTransactionList()
    {
        TransactionList.Clear();
        foreach (var transaction in books.Transactions)
        {
            TransactionList.Add(new TransactionViewModel(transaction));
        }
    }
}
