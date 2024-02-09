using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GneissBooks.Saft;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace GneissBooks.ViewModels;

public partial class AmazonStatementProcessingViewModel : ViewModelBase
{

    [ObservableProperty]
    private ErrorViewModel? _errorViewModel;

    [ObservableProperty]
    private string _documentPath = "";

    [ObservableProperty]
    private decimal _totalAmount = 0m;

    [ObservableProperty]
    private CurrencyViewModel? _totalAmountCurrency = null;

    [ObservableProperty]
    private decimal _fee = 0m;

    [ObservableProperty]
    private CurrencyViewModel? _feeCurrency = null;

    [ObservableProperty]
    private decimal _refunds = 0m;

    [ObservableProperty]
    private CurrencyViewModel? _refundsCurrency = null;

    [ObservableProperty]
    private DateTimeOffset _date = DateTimeOffset.Now;

    [ObservableProperty]
    private EntityViewModel? _selectedCustomer;

    [ObservableProperty]
    private int _numberOfHelios = 0; // todo make generic stock system
    [ObservableProperty]
    private int _numberOfHeliosAndCables = 0; // todo make generic stock system

    public IEnumerable<CurrencyViewModel> Currencies => MainViewModel.Currencies;

    public MainViewModel MainViewModel { get; private set; }


    public AmazonStatementProcessingViewModel()
    {
        MainViewModel = new MainViewModel();
    }

    public AmazonStatementProcessingViewModel(MainViewModel mainViewModel)
    {
        this.MainViewModel = mainViewModel;
        SelectedCustomer = MainViewModel.CustomerList.FirstOrDefault(customer => { return customer.CompanyName == "Amazon"; });
        FeeCurrency = MainViewModel.Currencies.FirstOrDefault(currency => { return currency.CurrencyCode == "USD"; });
        TotalAmountCurrency = FeeCurrency;
        RefundsCurrency = FeeCurrency;
    }

    [RelayCommand]
    public async Task Submit()
    {
        ErrorViewModel = null;

        try
        {
            if (RefundsCurrency != FeeCurrency || TotalAmountCurrency != FeeCurrency || RefundsCurrency != TotalAmountCurrency)
                throw new Exception("Not yet supported to have different currencies for each line");
            if (!File.Exists(DocumentPath))
                throw new Exception("Must specify document");
            //if (SelectedCustomer == null)
            //    throw new Exception("Must specify customer (likely Amazon)");

            var transactionLines = new List<TransactionLine>(); 

            transactionLines.Add(new TransactionLine(-TotalAmount, "3100", currency: TotalAmountCurrency?.CurrencyCode, description: "Salgssammendrag", taxCode: StandardTaxCodes.Export));
            if (Fee > 0)
                transactionLines.Add(new TransactionLine(Fee, "7300", currency: FeeCurrency?.CurrencyCode, description: "Provisjon", taxCode: StandardTaxCodes.NoTax));
            if (Refunds > 0)
                transactionLines.Add(new TransactionLine(Refunds, "7550", currency: RefundsCurrency?.CurrencyCode, description: "Erstatning/garanti"));
            
            transactionLines.Add(new TransactionLine(TotalAmount - Fee - Refunds, "1504", currency: TotalAmountCurrency?.CurrencyCode, description: "Salgssammendrag", customerId: SelectedCustomer?.SupplierCustomerId));
            
            var costOfGoods = NumberOfHelios * MainViewModel.HeliosProductionCost + NumberOfHeliosAndCables * (MainViewModel.HeliosProductionCost + MainViewModel.CableProductionCost);
            if (costOfGoods > 0)
            {
                transactionLines.Add(new TransactionLine(-costOfGoods, "1420", description: "Endring varelager"));
                transactionLines.Add(new TransactionLine(costOfGoods, "4100", description: "Forbruk varer/deler"));
            }

            var documentId = await MainViewModel.Books.AddTransaction(Date, "Salgssammendrag FBA", transactionLines);
            File.Move(DocumentPath, Path.Combine(Path.GetDirectoryName(DocumentPath)!, documentId + Path.GetExtension(DocumentPath)));

            await MainViewModel.RefreshTransactionList();
            await MainViewModel.RefreshCustomerList();
            await MainViewModel.RefreshSupplierList();

            TotalAmount = 0;
            Fee = 0;

        }
        catch (Exception ex)
        {
            ErrorViewModel = new(ex);
        }
    }

}
