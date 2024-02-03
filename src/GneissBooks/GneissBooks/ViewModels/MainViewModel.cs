using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rystem.OpenAi;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
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
    private ObservableCollection<EntityViewModel> _customerList = new();
    [ObservableProperty]
    private ObservableCollection<EntityViewModel> _supplierList = new();
    [ObservableProperty]
    private ObservableCollection<AccountViewModel> _accountList = new();
    [ObservableProperty]
    private ObservableCollection<TaxClassViewModel> _taxClassList = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedTransaction))]
    private TransactionViewModel? _selectedTransaction;

    [ObservableProperty]
    private EntityViewModel? _selectedCustomer;
    [ObservableProperty]
    private EntityViewModel? _selectedSupplier;
    [ObservableProperty]
    private TaxClassViewModel? _selectedTaxClass;
    [ObservableProperty]
    private AccountViewModel? _selectedAccount;

    [ObservableProperty]
    public TransactionViewModel _newManualTransaction;
    [ObservableProperty]
    private EntityViewModel? _newCustomer;
    [ObservableProperty]
    private EntityViewModel? _newSupplier;

    public bool HasSelectedTransaction => SelectedTransaction != null || Design.IsDesignMode;

    public static List<CountryViewModel> Countries = new();
    public static List<CurrencyViewModel> Currencies = new();

    public SaftBooks Books { get; private set; } = new();

    OpenAiApi openAi = new();

    decimal heliosProductionCost = 265.85m;
    decimal cableProductionCost = 51.49m;

    public MainViewModel()
    {
        FillStaticHelperLists();
        RefreshViewModelListsFromBooks().Wait();
        ResetNewTransactionForm();
        NewCustomer = new(this);
        NewSupplier = new(this);
    }

    [RelayCommand]
    public async Task LoadSaftFile()
    {
        try
        {
            Books.Load(SaftFileImportPath);
            await RefreshViewModelListsFromBooks();
            ResetNewTransactionForm();
            SelectedCustomer = null;
            SelectedSupplier = null;
            SelectedAccount = null;
            NewCustomer = new(this);
            NewSupplier = new(this);
        }
        catch { }
    }

    [RelayCommand]
    public async Task SaveSaftFile()
    {
        try
        {
            Books.Save(SaftFileExportPath);
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
        var response = await openAi.ChatAndReceiveResponse(invoiceText, "You are a bookkeeping robot parsing sales invoices for the company Mikkelsen Innovasjon. We sell the following products: Helios Laser DAC (SKU \"helios\"), ILDA cable (SKU \"db25\"), and LaserShowGen software (SKU \"lsg\"). You will be given pasted raw text from an invoice, and you are to respond with the following extracted information in json format: \"order_sum\", \"currency_code\", \"invoice_date\", \"payment_method\", \"buyer_first_name\", \"buyer_last_name\", \"buyer_country\", \"buyer_post_code\", \"buyer_city\", \"buyer_street_name\", \"buyer_street_number\", \"buyer_phone\", \"buyer_email\", \"buyer_company_name\", \"product_helios_quantity\", \"product_db25_quantity\", \"product_lsg_quantity\", \"order_number\", \"ebay_user\". All numerical fields should contain nothing but numbers. The payment method field should contain one of the following options: Stripe, Paypal, or Other. The invoice date should be in YYYY-MM-DD format. The invoice is either in USD or EUR currency. ebay_user can be empty if the order is not from Ebay. Other fields can only be empty if there is no applicable data for them in the invoice.");
        JsonNode bilagData = JsonNode.Parse(response)!;
        var sumInForeignCurrency = decimal.Parse(bilagData["order_sum"]!.ToString());
        var isEbay = invoiceText.ToLower().Contains("ebay order");
        var productionCost = int.Parse(bilagData["product_helios_quantity"]!.ToString()) * heliosProductionCost + int.Parse(bilagData["product_db25_quantity"]!.ToString()) * cableProductionCost;
        var currency = bilagData["currency_code"]!.ToString();
        var country = bilagData["buyer_country"]!.ToString().ToLower();
        var account = "1505";
        if (isEbay)
            account = "1503";
        else if (bilagData["payment_method"]!.ToString().ToLower().Contains("stripe"))
            account = "1502";
        else if (bilagData["payment_method"]!.ToString().ToLower().Contains("paypal"))
            account = "1501";

        var countryCode = FindCountryCodeFromCountryName(country) ?? "NO";

        var customer = await Books.FindOrAddCustomer(bilagData["buyer_company_name"]?.ToString(), bilagData["buyer_first_name"]?.ToString(), bilagData["buyer_last_name"]?.ToString(),
                                                    bilagData["buyer_post_code"]?.ToString(), bilagData["buyer_city"]?.ToString(), bilagData["buyer_street_name"]?.ToString(), bilagData["buyer_street_number"]?.ToString(),
                                                    null, countryCode, bilagData["buyer_phone"]?.ToString(), bilagData["buyer_email"]?.ToString());
        

        var lines = new List<TransactionLine>();

        lines.Add(new TransactionLine(sumInForeignCurrency, account, null, "Kundefordring", currency, customerId: customer));
        if (country == "no" || country == "norway")
            lines.Add(new TransactionLine(-sumInForeignCurrency, "3000", null, "Salgsinntekt", currency, StandardTaxCodes.OutgoingSaleTaxHighRate)); // Todo get proper tax codes from Books
        else
            lines.Add(new TransactionLine(-sumInForeignCurrency, "3100", null, "Salgsinntekt", currency, StandardTaxCodes.Export));
        if (productionCost > 0)
        {
            lines.Add(new TransactionLine(-productionCost, "1420", null, "Endring varelager"));
            lines.Add(new TransactionLine(productionCost, "4100", null, "Forbruk varer/deler"));
        }

        var documentId = await Books.AddTransaction(DateOnly.ParseExact(bilagData["invoice_date"]!.ToString(), "yyyy-MM-dd").ToDateTime(default), "Salg", lines);
        File.Move(path, Path.Combine(Path.GetDirectoryName(path)!, documentId + Path.GetExtension(path)));

        await RefreshCustomerList();
        await RefreshTransactionList();
        ResetNewTransactionForm();
    }

    [RelayCommand]
    public async Task CopyNewTransactionFromSelection()
    {
        NewManualTransaction = new(this);
        if (SelectedTransaction == null)
            return;

        NewManualTransaction.Description = SelectedTransaction.Description;
        NewManualTransaction.Date = SelectedTransaction.Date;
        foreach (var line in SelectedTransaction.Lines)
        {
            var newLine = new TransactionLineViewModel(this)
            {
                Currency = line.Currency,
                CurrencyExchangeRate = line.CurrencyExchangeRate,
                Amount = line.Amount,
                Description = line.Description,
                Customer = line.Customer,
                Supplier = line.Supplier,
                Account = line.Account,
                TaxClass = line.TaxClass,
                TaxBase = line.TaxBase
            };
            NewManualTransaction.Lines.Add(newLine);
        }
    }

    [RelayCommand]
    public async Task CopyNewCustomerFromSelection()
    {
        if (SelectedCustomer == null)
            NewCustomer = new(this);
        else
            NewCustomer = SelectedCustomer.GetCopy();
    }

    [RelayCommand]
    public async Task CopyNewSupplierFromSelection()
    {
        if (SelectedSupplier == null)
            NewSupplier = new(this);
        else
            NewSupplier = SelectedSupplier.GetCopy();
    }

    [RelayCommand]
    public async Task AddOrModifyNewCustomer()
    {
        if (NewCustomer == null)
            return;

        await Books.FindOrAddCustomer(NewCustomer.CompanyName, NewCustomer.FirstName, NewCustomer.LastName, NewCustomer.PostCode, NewCustomer.City, NewCustomer.StreetName, NewCustomer.StreetNumber,
            NewCustomer.AddressLine2, FindCountryCodeFromCountryName(NewCustomer.Country), NewCustomer.Telephone, NewCustomer.Email, NewCustomer.OpeningBalanceNumeric, NewCustomer.SupplierCustomerId);
        await RefreshCustomerList();
        SelectedCustomer = null;
        await CopyNewCustomerFromSelection();
    }

    [RelayCommand]
    public async Task AddOrModifyNewSupplier()
    {
        if (NewSupplier == null)
            return;

        await Books.FindOrAddSupplier(NewSupplier.CompanyName, NewSupplier.FirstName, NewSupplier.LastName, NewSupplier.PostCode, NewSupplier.City, NewSupplier.StreetName, NewSupplier.StreetNumber,
            NewSupplier.AddressLine2, FindCountryCodeFromCountryName(NewSupplier.Country), NewSupplier.Telephone, NewSupplier.Email, NewSupplier.OpeningBalanceNumeric, NewSupplier.SupplierCustomerId);
        await RefreshSupplierList();
        SelectedSupplier = null;
        await CopyNewSupplierFromSelection();
    }

    [RelayCommand]
    public async Task AddNewManualTransaction()
    {
        if (string.IsNullOrWhiteSpace(NewManualTransaction.DocumentPath) || !Path.Exists(NewManualTransaction.DocumentPath))
            throw new Exception("Need to choose source document.");

        var lines = new List<TransactionLine>();
        decimal totalAmount = 0;
        foreach (var line in NewManualTransaction.Lines)
        {
            if (!decimal.TryParse(line.Amount, out decimal amount))
                throw new Exception("Invalid amount in line: " + line.Amount);
            if (line.Account?.AccountId is not string accountId || accountId.Length != 4)
                throw new Exception("Invalid account in line: " + line.Account);

            totalAmount += amount;
            lines.Add(new TransactionLine(amount, accountId, line.TaxBase, line.Description, line.Currency?.CurrencyCode, line.TaxClass?.TaxCode, line.Customer?.SupplierCustomerId, line.Supplier?.SupplierCustomerId));
        }
        if (totalAmount != 0)
            throw new Exception("Debit and credit amounts do not cancel out. Double check balance.");

        var documentId = await Books.AddTransaction(NewManualTransaction.Date, NewManualTransaction.Description, lines);
        File.Move(NewManualTransaction.DocumentPath, Path.Combine(Path.GetDirectoryName(NewManualTransaction.DocumentPath)!, documentId + Path.GetExtension(NewManualTransaction.DocumentPath)));

        ResetNewTransactionForm();
        await RefreshTransactionList();
        await RefreshCustomerList();
        await RefreshSupplierList();
    }

    [RelayCommand]
    public async Task ResetNewCustomerForm()
    {
        SelectedCustomer = null;
        await CopyNewCustomerFromSelection();
    }

    [RelayCommand]
    public async Task ResetNewSupplierForm()
    {
        SelectedSupplier = null;
        await CopyNewSupplierFromSelection();
    }

    private void ResetNewTransactionForm()
    {
        NewManualTransaction = new(this);
        NewManualTransaction.Lines.Add(new(this));
        SelectedTransaction = null;
    }

    public async Task RefreshTransactionList()
    {
        TransactionList.Clear();
        foreach (var transaction in Books.Transactions)
        {
            TransactionList.Add(new TransactionViewModel(transaction, this));
        }
    }

    public async Task RefreshCustomerList()
    {
        CustomerList.Clear();
        foreach (var customer in Books.Customers)
        {
            CustomerList.Add(new EntityViewModel(customer, this));
        }
    }

    public async Task RefreshSupplierList()
    {
        SupplierList.Clear();
        foreach (var supplier in Books.Suppliers)
        {
            SupplierList.Add(new EntityViewModel(supplier, this));
        }
    }

    public async Task RefreshAccountList()
    {
        AccountList.Clear();
        foreach (var account in Books.Accounts)
        {
            AccountList.Add(new AccountViewModel(account));
        }
    }

    public async Task RefreshTaxClassList()
    {
        TaxClassList.Clear();
        foreach (var taxClass in Books.TaxClasses)
        {
            TaxClassList.Add(new TaxClassViewModel(taxClass));
        }
    }

    public async Task RefreshViewModelListsFromBooks()
    {
        await RefreshAccountList();
        await RefreshCustomerList();
        await RefreshSupplierList();
        await RefreshTaxClassList();
        await RefreshTransactionList();
    }

    static public string FindCountryCodeFromCountryName(string? countryName)
    {
        if (countryName == null)
            return "NO";
        if (countryName.Length == 2)
            return countryName.ToUpper();
        countryName = countryName.ToUpper();
        if (countryName.Length != 0)
            return Countries.FirstOrDefault(_country => _country.Name.Contains(countryName))?.CountryCode ?? "NO";
        
        return "NO";
    }

    [RelayCommand]
    public async Task CreateNewTransaction()
    {
        NewManualTransaction = new TransactionViewModel(this);
        NewManualTransaction.Lines.Add(new(this));
    }

    partial void OnSelectedTransactionChanged(TransactionViewModel? value)
    {
        if (HasSelectedTransaction)
            CopyNewTransactionFromSelection().Wait();
    }

    partial void OnSelectedCustomerChanged(EntityViewModel? value)
    {
        if (SelectedCustomer != null)
            CopyNewCustomerFromSelection().Wait();
    }

    partial void OnSelectedSupplierChanged(EntityViewModel? value)
    {
        if (SelectedSupplier != null)
            CopyNewSupplierFromSelection().Wait();
    }

    void FillStaticHelperLists()
    {
        Countries = new()
        {
            new CountryViewModel("Default", null),
            new CountryViewModel("AFGHANISTAN", "AF"),
            new CountryViewModel("ÅLAND ISLANDS", "AX"),
            new CountryViewModel("ALBANIA", "AL"),
            new CountryViewModel("ALGERIA", "DZ"),
            new CountryViewModel("AMERICAN SAMOA", "AS"),
            new CountryViewModel("ANDORRA", "AD"),
            new CountryViewModel("ANGOLA", "AO"),
            new CountryViewModel("ANGUILLA", "AI"),
            new CountryViewModel("ANTARCTICA", "AQ"),
            new CountryViewModel("ANTIGUA AND BARBUDA", "AG"),
            new CountryViewModel("ARGENTINA", "AR"),
            new CountryViewModel("ARMENIA", "AM"),
            new CountryViewModel("ARUBA", "AW"),
            new CountryViewModel("AUSTRALIA", "AU"),
            new CountryViewModel("AUSTRIA", "AT"),
            new CountryViewModel("AZERBAIJAN", "AZ"),
            new CountryViewModel("BAHAMAS", "BS"),
            new CountryViewModel("BAHRAIN", "BH"),
            new CountryViewModel("BANGLADESH", "BD"),
            new CountryViewModel("BARBADOS", "BB"),
            new CountryViewModel("BELARUS", "BY"),
            new CountryViewModel("BELGIUM", "BE"),
            new CountryViewModel("BELIZE", "BZ"),
            new CountryViewModel("BENIN", "BJ"),
            new CountryViewModel("BERMUDA", "BM"),
            new CountryViewModel("BHUTAN", "BT"),
            new CountryViewModel("BOLIVIA, PLURINATIONAL STATE OF", "BO"),
            new CountryViewModel("BONAIRE, SINT EUSTATIUS AND SABA", "BQ"),
            new CountryViewModel("BOSNIA AND HERZEGOVINA", "BA"),
            new CountryViewModel("BOTSWANA", "BW"),
            new CountryViewModel("BOUVET ISLAND", "BV"),
            new CountryViewModel("BRAZIL", "BR"),
            new CountryViewModel("BRITISH INDIAN OCEAN TERRITORY", "IO"),
            new CountryViewModel("BRUNEI DARUSSALAM", "BN"),
            new CountryViewModel("BULGARIA", "BG"),
            new CountryViewModel("BURKINA FASO", "BF"),
            new CountryViewModel("BURUNDI", "BI"),
            new CountryViewModel("CAMBODIA", "KH"),
            new CountryViewModel("CAMEROON", "CM"),
            new CountryViewModel("CANADA", "CA"),
            new CountryViewModel("CAPE VERDE", "CV"),
            new CountryViewModel("CAYMAN ISLANDS", "KY"),
            new CountryViewModel("CENTRAL AFRICAN REPUBLIC", "CF"),
            new CountryViewModel("CHAD", "TD"),
            new CountryViewModel("CHILE", "CL"),
            new CountryViewModel("CHINA", "CN"),
            new CountryViewModel("CHRISTMAS ISLAND", "CX"),
            new CountryViewModel("COCOS (KEELING) ISLANDS", "CC"),
            new CountryViewModel("COLOMBIA", "CO"),
            new CountryViewModel("COMOROS", "KM"),
            new CountryViewModel("CONGO", "CG"),
            new CountryViewModel("CONGO, THE DEMOCRATIC REPUBLIC OF THE", "CD"),
            new CountryViewModel("COOK ISLANDS", "CK"),
            new CountryViewModel("COSTA RICA", "CR"),
            new CountryViewModel("CÔTE D'IVOIRE", "CI"),
            new CountryViewModel("CROATIA", "HR"),
            new CountryViewModel("CUBA", "CU"),
            new CountryViewModel("CURAÇAO", "CW"),
            new CountryViewModel("CYPRUS", "CY"),
            new CountryViewModel("CZECH REPUBLIC", "CZ"),
            new CountryViewModel("DENMARK", "DK"),
            new CountryViewModel("DJIBOUTI", "DJ"),
            new CountryViewModel("DOMINICA", "DM"),
            new CountryViewModel("DOMINICAN REPUBLIC", "DO"),
            new CountryViewModel("ECUADOR", "EC"),
            new CountryViewModel("EGYPT", "EG"),
            new CountryViewModel("EL SALVADOR", "SV"),
            new CountryViewModel("EQUATORIAL GUINEA", "GQ"),
            new CountryViewModel("ERITREA", "ER"),
            new CountryViewModel("ESTONIA", "EE"),
            new CountryViewModel("ETHIOPIA", "ET"),
            new CountryViewModel("FALKLAND ISLANDS (MALVINAS)", "FK"),
            new CountryViewModel("FAROE ISLANDS", "FO"),
            new CountryViewModel("FIJI", "FJ"),
            new CountryViewModel("FINLAND", "FI"),
            new CountryViewModel("FRANCE", "FR"),
            new CountryViewModel("FRENCH GUIANA", "GF"),
            new CountryViewModel("FRENCH POLYNESIA", "PF"),
            new CountryViewModel("FRENCH SOUTHERN TERRITORIES", "TF"),
            new CountryViewModel("GABON", "GA"),
            new CountryViewModel("GAMBIA", "GM"),
            new CountryViewModel("GEORGIA", "GE"),
            new CountryViewModel("GERMANY", "DE"),
            new CountryViewModel("GHANA", "GH"),
            new CountryViewModel("GIBRALTAR", "GI"),
            new CountryViewModel("GREECE", "GR"),
            new CountryViewModel("GREENLAND", "GL"),
            new CountryViewModel("GRENADA", "GD"),
            new CountryViewModel("GUADELOUPE", "GP"),
            new CountryViewModel("GUAM", "GU"),
            new CountryViewModel("GUATEMALA", "GT"),
            new CountryViewModel("GUERNSEY", "GG"),
            new CountryViewModel("GUINEA", "GN"),
            new CountryViewModel("GUINEA-BISSAU", "GW"),
            new CountryViewModel("GUYANA", "GY"),
            new CountryViewModel("HAITI", "HT"),
            new CountryViewModel("HEARD ISLAND AND MCDONALD ISLANDS", "HM"),
            new CountryViewModel("HOLY SEE (VATICAN CITY STATE)", "VA"),
            new CountryViewModel("HONDURAS", "HN"),
            new CountryViewModel("HONG KONG", "HK"),
            new CountryViewModel("HUNGARY", "HU"),
            new CountryViewModel("ICELAND", "IS"),
            new CountryViewModel("INDIA", "IN"),
            new CountryViewModel("INDONESIA", "ID"),
            new CountryViewModel("IRAN, ISLAMIC REPUBLIC OF", "IR"),
            new CountryViewModel("IRAQ", "IQ"),
            new CountryViewModel("IRELAND", "IE"),
            new CountryViewModel("ISLE OF MAN", "IM"),
            new CountryViewModel("ISRAEL", "IL"),
            new CountryViewModel("ITALY", "IT"),
            new CountryViewModel("JAMAICA", "JM"),
            new CountryViewModel("JAPAN", "JP"),
            new CountryViewModel("JERSEY", "JE"),
            new CountryViewModel("JORDAN", "JO"),
            new CountryViewModel("KAZAKHSTAN", "KZ"),
            new CountryViewModel("KENYA", "KE"),
            new CountryViewModel("KIRIBATI", "KI"),
            new CountryViewModel("KOREA, DEMOCRATIC PEOPLE'S REPUBLIC OF", "KP"),
            new CountryViewModel("KOREA, REPUBLIC OF", "KR"),
            new CountryViewModel("KUWAIT", "KW"),
            new CountryViewModel("KYRGYZSTAN", "KG"),
            new CountryViewModel("LAO PEOPLE'S DEMOCRATIC REPUBLIC", "LA"),
            new CountryViewModel("LATVIA", "LV"),
            new CountryViewModel("LEBANON", "LB"),
            new CountryViewModel("LESOTHO", "LS"),
            new CountryViewModel("LIBERIA", "LR"),
            new CountryViewModel("LIBYA", "LY"),
            new CountryViewModel("LIECHTENSTEIN", "LI"),
            new CountryViewModel("LITHUANIA", "LT"),
            new CountryViewModel("LUXEMBOURG", "LU"),
            new CountryViewModel("MACAO", "MO"),
            new CountryViewModel("MACEDONIA, THE FORMER YUGOSLAV REPUBLIC OF", "MK"),
            new CountryViewModel("MADAGASCAR", "MG"),
            new CountryViewModel("MALAWI", "MW"),
            new CountryViewModel("MALAYSIA", "MY"),
            new CountryViewModel("MALDIVES", "MV"),
            new CountryViewModel("MALI", "ML"),
            new CountryViewModel("MALTA", "MT"),
            new CountryViewModel("MARSHALL ISLANDS", "MH"),
            new CountryViewModel("MARTINIQUE", "MQ"),
            new CountryViewModel("MAURITANIA", "MR"),
            new CountryViewModel("MAURITIUS", "MU"),
            new CountryViewModel("MAYOTTE", "YT"),
            new CountryViewModel("MEXICO", "MX"),
            new CountryViewModel("MICRONESIA, FEDERATED STATES OF", "FM"),
            new CountryViewModel("MOLDOVA, REPUBLIC OF", "MD"),
            new CountryViewModel("MONACO", "MC"),
            new CountryViewModel("MONGOLIA", "MN"),
            new CountryViewModel("MONTENEGRO", "ME"),
            new CountryViewModel("MONTSERRAT", "MS"),
            new CountryViewModel("MOROCCO", "MA"),
            new CountryViewModel("MOZAMBIQUE", "MZ"),
            new CountryViewModel("MYANMAR", "MM"),
            new CountryViewModel("NAMIBIA", "NA"),
            new CountryViewModel("NAURU", "NR"),
            new CountryViewModel("NEPAL", "NP"),
            new CountryViewModel("NETHERLANDS", "NL"),
            new CountryViewModel("NEW CALEDONIA", "NC"),
            new CountryViewModel("NEW ZEALAND", "NZ"),
            new CountryViewModel("NICARAGUA", "NI"),
            new CountryViewModel("NIGER", "NE"),
            new CountryViewModel("NIGERIA", "NG"),
            new CountryViewModel("NIUE", "NU"),
            new CountryViewModel("NORFOLK ISLAND", "NF"),
            new CountryViewModel("NORTHERN MARIANA ISLANDS", "MP"),
            new CountryViewModel("NORWAY", "NO"),
            new CountryViewModel("OMAN", "OM"),
            new CountryViewModel("PAKISTAN", "PK"),
            new CountryViewModel("PALAU", "PW"),
            new CountryViewModel("PALESTINE, STATE OF", "PS"),
            new CountryViewModel("PANAMA", "PA"),
            new CountryViewModel("PAPUA NEW GUINEA", "PG"),
            new CountryViewModel("PARAGUAY", "PY"),
            new CountryViewModel("PERU", "PE"),
            new CountryViewModel("PHILIPPINES", "PH"),
            new CountryViewModel("PITCAIRN", "PN"),
            new CountryViewModel("POLAND", "PL"),
            new CountryViewModel("PORTUGAL", "PT"),
            new CountryViewModel("PUERTO RICO", "PR"),
            new CountryViewModel("QATAR", "QA"),
            new CountryViewModel("RÉUNION", "RE"),
            new CountryViewModel("ROMANIA", "RO"),
            new CountryViewModel("RUSSIAN FEDERATION", "RU"),
            new CountryViewModel("RWANDA", "RW"),
            new CountryViewModel("SAINT BARTHÉLEMY", "BL"),
            new CountryViewModel("SAINT HELENA, ASCENSION AND TRISTAN DA CUNHA", "SH"),
            new CountryViewModel("SAINT KITTS AND NEVIS", "KN"),
            new CountryViewModel("SAINT LUCIA", "LC"),
            new CountryViewModel("SAINT MARTIN (FRENCH PART)", "MF"),
            new CountryViewModel("SAINT PIERRE AND MIQUELON", "PM"),
            new CountryViewModel("SAINT VINCENT AND THE GRENADINES", "VC"),
            new CountryViewModel("SAMOA", "WS"),
            new CountryViewModel("SAN MARINO", "SM"),
            new CountryViewModel("SAO TOME AND PRINCIPE", "ST"),
            new CountryViewModel("SAUDI ARABIA", "SA"),
            new CountryViewModel("SENEGAL", "SN"),
            new CountryViewModel("SERBIA", "RS"),
            new CountryViewModel("SEYCHELLES", "SC"),
            new CountryViewModel("SIERRA LEONE", "SL"),
            new CountryViewModel("SINGAPORE", "SG"),
            new CountryViewModel("SINT MAARTEN (DUTCH PART)", "SX"),
            new CountryViewModel("SLOVAKIA", "SK"),
            new CountryViewModel("SLOVENIA", "SI"),
            new CountryViewModel("SOLOMON ISLANDS", "SB"),
            new CountryViewModel("SOMALIA", "SO"),
            new CountryViewModel("SOUTH AFRICA", "ZA"),
            new CountryViewModel("SOUTH GEORGIA AND THE SOUTH SANDWICH ISLANDS", "GS"),
            new CountryViewModel("SOUTH SUDAN", "SS"),
            new CountryViewModel("SPAIN", "ES"),
            new CountryViewModel("SRI LANKA", "LK"),
            new CountryViewModel("SUDAN", "SD"),
            new CountryViewModel("SURINAME", "SR"),
            new CountryViewModel("SVALBARD AND JAN MAYEN", "SJ"),
            new CountryViewModel("SWAZILAND", "SZ"),
            new CountryViewModel("SWEDEN", "SE"),
            new CountryViewModel("SWITZERLAND", "CH"),
            new CountryViewModel("SYRIAN ARAB REPUBLIC", "SY"),
            new CountryViewModel("TAIWAN, PROVINCE OF CHINA", "TW"),
            new CountryViewModel("TAJIKISTAN", "TJ"),
            new CountryViewModel("TANZANIA, UNITED REPUBLIC OF", "TZ"),
            new CountryViewModel("THAILAND", "TH"),
            new CountryViewModel("TIMOR-LESTE", "TL"),
            new CountryViewModel("TOGO", "TG"),
            new CountryViewModel("TOKELAU", "TK"),
            new CountryViewModel("TONGA", "TO"),
            new CountryViewModel("TRINIDAD AND TOBAGO", "TT"),
            new CountryViewModel("TUNISIA", "TN"),
            new CountryViewModel("TURKEY", "TR"),
            new CountryViewModel("TURKMENISTAN", "TM"),
            new CountryViewModel("TURKS AND CAICOS ISLANDS", "TC"),
            new CountryViewModel("TUVALU", "TV"),
            new CountryViewModel("UGANDA", "UG"),
            new CountryViewModel("UKRAINE", "UA"),
            new CountryViewModel("UNITED ARAB EMIRATES", "AE"),
            new CountryViewModel("UNITED KINGDOM", "GB"),
            new CountryViewModel("UNITED STATES", "US"),
            new CountryViewModel("UNITED STATES MINOR OUTLYING ISLANDS", "UM"),
            new CountryViewModel("URUGUAY", "UY"),
            new CountryViewModel("UZBEKISTAN", "UZ"),
            new CountryViewModel("VANUATU", "VU"),
            new CountryViewModel("VENEZUELA, BOLIVARIAN REPUBLIC OF", "VE"),
            new CountryViewModel("VIET NAM", "VN"),
            new CountryViewModel("VIRGIN ISLANDS, BRITISH", "VG"),
            new CountryViewModel("VIRGIN ISLANDS, U.S.", "VI"),
            new CountryViewModel("WALLIS AND FUTUNA", "WF"),
            new CountryViewModel("WESTERN SAHARA", "EH"),
            new CountryViewModel("YEMEN", "YE"),
            new CountryViewModel("ZAMBIA", "ZM"),
            new CountryViewModel("ZIMBABWE", "ZW")
        };

        Currencies = new()
        {
            new CurrencyViewModel(null,"","2","Default"),
            new CurrencyViewModel("AED","784","2","United Arab Emirates dirham"),
            new CurrencyViewModel("AFN","971","2","Afghan afghani"),
            new CurrencyViewModel("ALL","8","2","Albanian lek"),
            new CurrencyViewModel("AMD","51","2","Armenian dram"),
            new CurrencyViewModel("ANG","532","2","Netherlands Antillean guilder"),
            new CurrencyViewModel("AOA","973","2","Angolan kwanza"),
            new CurrencyViewModel("ARS","32","2","Argentine peso"),
            new CurrencyViewModel("AUD","36","2","Australian dollar"),
            new CurrencyViewModel("AWG","533","2","Aruban florin"),
            new CurrencyViewModel("AZN","944","2","Azerbaijani manat"),
            new CurrencyViewModel("BAM","977","2","Bosnia and Herzegovina convertible mark"),
            new CurrencyViewModel("BBD","52","2","Barbados dollar"),
            new CurrencyViewModel("BDT","50","2","Bangladeshi taka"),
            new CurrencyViewModel("BGN","975","2","Bulgarian lev"),
            new CurrencyViewModel("BHD","48","3","Bahraini dinar"),
            new CurrencyViewModel("BIF","108","0","Burundian franc"),
            new CurrencyViewModel("BMD","60","2","Bermudian dollar (customarily known as Bermuda dollar),"),
            new CurrencyViewModel("BND","96","2","Brunei dollar"),
            new CurrencyViewModel("BOB","68","2","Boliviano"),
            new CurrencyViewModel("BOV","984","2","Bolivian Mvdol (funds code),"),
            new CurrencyViewModel("BRL","986","2","Brazilian real"),
            new CurrencyViewModel("BSD","44","2","Bahamian dollar"),
            new CurrencyViewModel("BTN","64","2","Bhutanese ngultrum"),
            new CurrencyViewModel("BWP","72","2","Botswana pula"),
            new CurrencyViewModel("BYR","974","0","Belarusian ruble"),
            new CurrencyViewModel("BZD","84","2","Belize dollar"),
            new CurrencyViewModel("CAD","124","2","Canadian dollar"),
            new CurrencyViewModel("CDF","976","2","Congolese franc"),
            new CurrencyViewModel("CHE","947","2","WIR Euro (complementary currency),"),
            new CurrencyViewModel("CHF","756","2","Swiss franc"),
            new CurrencyViewModel("CHW","948","2","WIR Franc (complementary currency),"),
            new CurrencyViewModel("CLF","990","0","Unidad de Fomento (funds code),"),
            new CurrencyViewModel("CLP","152","0","Chilean peso"),
            new CurrencyViewModel("CNY","156","2","Chinese yuan"),
            new CurrencyViewModel("COP","170","2","Colombian peso"),
            new CurrencyViewModel("COU","970","2","Unidad de Valor Real"),
            new CurrencyViewModel("CRC","188","2","Costa Rican colon"),
            new CurrencyViewModel("CUC","931","2","Cuban convertible peso"),
            new CurrencyViewModel("CUP","192","2","Cuban peso"),
            new CurrencyViewModel("CVE","132","0","Cape Verde escudo"),
            new CurrencyViewModel("CZK","203","2","Czech koruna"),
            new CurrencyViewModel("DJF","262","0","Djiboutian franc"),
            new CurrencyViewModel("DKK","208","2","Danish krone"),
            new CurrencyViewModel("DOP","214","2","Dominican peso"),
            new CurrencyViewModel("DZD","12","2","Algerian dinar"),
            new CurrencyViewModel("EGP","818","2","Egyptian pound"),
            new CurrencyViewModel("ERN","232","2","Eritrean nakfa"),
            new CurrencyViewModel("ETB","230","2","Ethiopian birr"),
            new CurrencyViewModel("EUR","978","2","Euro"),
            new CurrencyViewModel("FJD","242","2","Fiji dollar"),
            new CurrencyViewModel("FKP","238","2","Falkland Islands pound"),
            new CurrencyViewModel("GBP","826","2","Pound sterling"),
            new CurrencyViewModel("GEL","981","2","Georgian lari"),
            new CurrencyViewModel("GHS","936","2","Ghanaian cedi"),
            new CurrencyViewModel("GIP","292","2","Gibraltar pound"),
            new CurrencyViewModel("GMD","270","2","Gambian dalasi"),
            new CurrencyViewModel("GNF","324","0","Guinean franc"),
            new CurrencyViewModel("GTQ","320","2","Guatemalan quetzal"),
            new CurrencyViewModel("GYD","328","2","Guyanese dollar"),
            new CurrencyViewModel("HKD","344","2","Hong Kong dollar"),
            new CurrencyViewModel("HNL","340","2","Honduran lempira"),
            new CurrencyViewModel("HRK","191","2","Croatian kuna"),
            new CurrencyViewModel("HTG","332","2","Haitian gourde"),
            new CurrencyViewModel("HUF","348","2","Hungarian forint"),
            new CurrencyViewModel("IDR","360","2","Indonesian rupiah"),
            new CurrencyViewModel("ILS","376","2","Israeli new shekel"),
            new CurrencyViewModel("INR","356","2","Indian rupee"),
            new CurrencyViewModel("IQD","368","3","Iraqi dinar"),
            new CurrencyViewModel("IRR","364","0","Iranian rial"),
            new CurrencyViewModel("ISK","352","0","Icelandic króna"),
            new CurrencyViewModel("JMD","388","2","Jamaican dollar"),
            new CurrencyViewModel("JOD","400","3","Jordanian dinar"),
            new CurrencyViewModel("JPY","392","0","Japanese yen"),
            new CurrencyViewModel("KES","404","2","Kenyan shilling"),
            new CurrencyViewModel("KGS","417","2","Kyrgyzstani som"),
            new CurrencyViewModel("KHR","116","2","Cambodian riel"),
            new CurrencyViewModel("KMF","174","0","Comoro franc"),
            new CurrencyViewModel("KPW","408","0","North Korean won"),
            new CurrencyViewModel("KRW","410","0","South Korean won"),
            new CurrencyViewModel("KWD","414","3","Kuwaiti dinar"),
            new CurrencyViewModel("KYD","136","2","Cayman Islands dollar"),
            new CurrencyViewModel("KZT","398","2","Kazakhstani tenge"),
            new CurrencyViewModel("LAK","418","0","Lao kip"),
            new CurrencyViewModel("LBP","422","0","Lebanese pound"),
            new CurrencyViewModel("LKR","144","2","Sri Lankan rupee"),
            new CurrencyViewModel("LRD","430","2","Liberian dollar"),
            new CurrencyViewModel("LSL","426","2","Lesotho loti"),
            new CurrencyViewModel("LTL","440","2","Lithuanian litas"),
            new CurrencyViewModel("LVL","428","2","Latvian lats"),
            new CurrencyViewModel("LYD","434","3","Libyan dinar"),
            new CurrencyViewModel("MAD","504","2","Moroccan dirham"),
            new CurrencyViewModel("MDL","498","2","Moldovan leu"),
            new CurrencyViewModel("MGA","969","0.7[8]","Malagasy ariary"),
            new CurrencyViewModel("MKD","807","0","Macedonian denar"),
            new CurrencyViewModel("MMK","104","0","Myanma kyat"),
            new CurrencyViewModel("MNT","496","2","Mongolian tugrik"),
            new CurrencyViewModel("MOP","446","2","Macanese pataca"),
            new CurrencyViewModel("MRO","478","0.7[8]","Mauritanian ouguiya"),
            new CurrencyViewModel("MUR","480","2","Mauritian rupee"),
            new CurrencyViewModel("MVR","462","2","Maldivian rufiyaa"),
            new CurrencyViewModel("MWK","454","2","Malawian kwacha"),
            new CurrencyViewModel("MXN","484","2","Mexican peso"),
            new CurrencyViewModel("MXV","979","2","Mexican Unidad de Inversion (UDI), (funds code),"),
            new CurrencyViewModel("MYR","458","2","Malaysian ringgit"),
            new CurrencyViewModel("MZN","943","2","Mozambican metical"),
            new CurrencyViewModel("NAD","516","2","Namibian dollar"),
            new CurrencyViewModel("NGN","566","2","Nigerian naira"),
            new CurrencyViewModel("NIO","558","2","Nicaraguan córdoba"),
            new CurrencyViewModel("NOK","578","2","Norwegian krone"),
            new CurrencyViewModel("NPR","524","2","Nepalese rupee"),
            new CurrencyViewModel("NZD","554","2","New Zealand dollar"),
            new CurrencyViewModel("OMR","512","3","Omani rial"),
            new CurrencyViewModel("PAB","590","2","Panamanian balboa"),
            new CurrencyViewModel("PEN","604","2","Peruvian nuevo sol"),
            new CurrencyViewModel("PGK","598","2","Papua New Guinean kina"),
            new CurrencyViewModel("PHP","608","2","Philippine peso"),
            new CurrencyViewModel("PKR","586","2","Pakistani rupee"),
            new CurrencyViewModel("PLN","985","2","Polish złoty"),
            new CurrencyViewModel("PYG","600","0","Paraguayan guaraní"),
            new CurrencyViewModel("QAR","634","2","Qatari riyal"),
            new CurrencyViewModel("RON","946","2","Romanian new leu"),
            new CurrencyViewModel("RSD","941","2","Serbian dinar"),
            new CurrencyViewModel("RUB","643","2","Russian rouble"),
            new CurrencyViewModel("RWF","646","0","Rwandan franc"),
            new CurrencyViewModel("SAR","682","2","Saudi riyal"),
            new CurrencyViewModel("SBD","90","2","Solomon Islands dollar"),
            new CurrencyViewModel("SCR","690","2","Seychelles rupee"),
            new CurrencyViewModel("SDG","938","2","Sudanese pound"),
            new CurrencyViewModel("SEK","752","2","Swedish krona/kronor"),
            new CurrencyViewModel("SGD","702","2","Singapore dollar"),
            new CurrencyViewModel("SHP","654","2","Saint Helena pound"),
            new CurrencyViewModel("SLL","694","0","Sierra Leonean leone"),
            new CurrencyViewModel("SOS","706","2","Somali shilling"),
            new CurrencyViewModel("SRD","968","2","Surinamese dollar"),
            new CurrencyViewModel("SSP","728","2","South Sudanese pound"),
            new CurrencyViewModel("STD","678","0","São Tomé and Príncipe dobra"),
            new CurrencyViewModel("SYP","760","2","Syrian pound"),
            new CurrencyViewModel("SZL","748","2","Swazi lilangeni"),
            new CurrencyViewModel("THB","764","2","Thai baht"),
            new CurrencyViewModel("TJS","972","2","Tajikistani somoni"),
            new CurrencyViewModel("TMT","934","2","Turkmenistani manat"),
            new CurrencyViewModel("TND","788","3","Tunisian dinar"),
            new CurrencyViewModel("TOP","776","2","Tongan paʻanga"),
            new CurrencyViewModel("TRY","949","2","Turkish lira"),
            new CurrencyViewModel("TTD","780","2","Trinidad and Tobago dollar"),
            new CurrencyViewModel("TWD","901","2","New Taiwan dollar"),
            new CurrencyViewModel("TZS","834","2","Tanzanian shilling"),
            new CurrencyViewModel("UAH","980","2","Ukrainian hryvnia"),
            new CurrencyViewModel("UGX","800","2","Ugandan shilling"),
            new CurrencyViewModel("USD","840","2","United States dollar"),
            new CurrencyViewModel("USN","997","2","United States dollar (next day), (funds code),"),
            new CurrencyViewModel("USS","998","2","United States dollar (same day), (funds code), (one source[who?] claims it is no longer used, but it is still on the ISO 4217-MA list),"),
            new CurrencyViewModel("UYI","940","0","Uruguay Peso en Unidades Indexadas (URUIURUI), (funds code),"),
            new CurrencyViewModel("UYU","858","2","Uruguayan peso"),
            new CurrencyViewModel("UZS","860","2","Uzbekistan som"),
            new CurrencyViewModel("VEF","937","2","Venezuelan bolívar fuerte"),
            new CurrencyViewModel("VND","704","0","Vietnamese dong"),
            new CurrencyViewModel("VUV","548","0","Vanuatu vatu"),
            new CurrencyViewModel("WST","882","2","Samoan tala"),
            new CurrencyViewModel("XAF","950","0","CFA franc BEAC"),
            new CurrencyViewModel("XAG","961",".","Silver (one troy ounce),"),
            new CurrencyViewModel("XAU","959",".","Gold (one troy ounce),"),
            new CurrencyViewModel("XBA","955",".","European Composite Unit (EURCO), (bond market unit),"),
            new CurrencyViewModel("XBB","956",".","European Monetary Unit (E.M.U.-6), (bond market unit),"),
            new CurrencyViewModel("XBC","957",".","European Unit of Account 9 (E.U.A.-9), (bond market unit),"),
            new CurrencyViewModel("XBD","958",".","European Unit of Account 17 (E.U.A.-17), (bond market unit),"),
            new CurrencyViewModel("XCD","951","2","East Caribbean dollar"),
            new CurrencyViewModel("XDR","960",".","Special drawing rights"),
            new CurrencyViewModel("XFU","Nil",".","UIC franc (special settlement currency),"),
            new CurrencyViewModel("XOF","952","0","CFA franc BCEAO"),
            new CurrencyViewModel("XPD","964",".","Palladium (one troy ounce),"),
            new CurrencyViewModel("XPF","953","0","CFP franc"),
            new CurrencyViewModel("XPT","962",".","Platinum (one troy ounce),"),
            new CurrencyViewModel("XTS","963",".","Code reserved for testing purposes"),
            new CurrencyViewModel("XXX","999",".","No currency"),
            new CurrencyViewModel("YER","886","2","Yemeni rial"),
            new CurrencyViewModel("ZAR","710","2","South African rand"),
            new CurrencyViewModel("ZMW","967","2","Zambian kwacha")
        };
    }
}

public class CountryViewModel
{
    public string Name { get; }
    public string? CountryCode { get; }

    public CountryViewModel(string name, string? countryCode)
    {
        Name = name;
        CountryCode = countryCode;
    }

    public override string ToString()
    {
        return $"{Name}";
    }
}

public class CurrencyViewModel
{
    public string Name { get; }
    public string? CurrencyCode { get; }

    public CurrencyViewModel(string? currencyCode, string number, string numberOfDecimals, string name)
    {
        Name = name;
        CurrencyCode = currencyCode;
    }

    public override string ToString()
    {
        return $"{CurrencyCode}";// ({Name})";
    }
}