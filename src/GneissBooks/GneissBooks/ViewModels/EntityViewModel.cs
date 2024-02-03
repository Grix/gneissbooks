﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GneissBooks.Saft;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace GneissBooks.ViewModels;

public partial class EntityViewModel : ViewModelBase
{
    public string Title => $"{SupplierCustomerId}: {CompanyName ?? ($"{FirstName} {LastName}")} ({PostCode}, {Country})";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    private string? _companyName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    private string? _firstName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    private string? _lastName;

    [ObservableProperty]
    private string? _registrationNumber;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    private string? _supplierCustomerId;

    [ObservableProperty]
    private string? _accountId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    private string? _postCode;

    [ObservableProperty]
    private string? _city;

    [ObservableProperty]
    private string? _streetName;

    [ObservableProperty]
    private string? _streetNumber;

    [ObservableProperty]
    private string? _addressLine2;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    private string? _country;

    [ObservableProperty]
    private string? _telephone;

    [ObservableProperty]
    private string? _email;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ClosingBalanceNumeric))]
    private string _closingBalance = "0.00";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OpeningBalanceNumeric))]
    [NotifyPropertyChangedFor(nameof(ClosingBalanceNumeric))]
    private string _openingBalance = "0.00";

    public decimal ClosingBalanceNumeric => decimal.TryParse(ClosingBalance, out decimal amountNumeric) ? amountNumeric : 0m;
    public decimal OpeningBalanceNumeric => decimal.TryParse(OpeningBalance, out decimal amountNumeric) ? amountNumeric : 0m;

    private MainViewModel mainViewModel;

    public EntityViewModel(MainViewModel mainViewModel)
    {
        this.mainViewModel = mainViewModel;
    }

    public EntityViewModel(CompanyStructure rawCompanyStructure, MainViewModel mainViewModel) 
    {
        this.mainViewModel = mainViewModel;

        CompanyName = rawCompanyStructure.Name;
        FirstName = rawCompanyStructure.Contact?.FirstOrDefault()?.ContactPerson?.FirstName;
        LastName = rawCompanyStructure.Contact?.FirstOrDefault()?.ContactPerson?.LastName;
        RegistrationNumber = rawCompanyStructure.RegistrationNumber;
        PostCode = rawCompanyStructure.Address?.FirstOrDefault()?.PostalCode;
        City = rawCompanyStructure.Address?.FirstOrDefault()?.City;
        StreetName = rawCompanyStructure.Address?.FirstOrDefault()?.StreetName;
        StreetNumber = rawCompanyStructure.Address?.FirstOrDefault()?.Number;
        AddressLine2 = rawCompanyStructure.Address?.FirstOrDefault()?.AdditionalAddressDetail;
        Country = rawCompanyStructure.Address?.FirstOrDefault()?.Country;
        Email = rawCompanyStructure.Contact?.FirstOrDefault()?.Email;
        Telephone = rawCompanyStructure.Contact?.FirstOrDefault()?.Telephone;

        if (rawCompanyStructure is AuditFileMasterFilesCustomer customer)
        {
            SupplierCustomerId = customer.CustomerID;
            AccountId = customer.AccountID;
            OpeningBalance = (customer.ItemElementName == ItemChoiceType1.OpeningDebitBalance ? customer.Item : -customer.Item).ToString("N2");
            ClosingBalance = (customer.Item1ElementName == Item1ChoiceType1.ClosingDebitBalance ? customer.Item1 : -customer.Item1).ToString("N2");
        }
        else if (rawCompanyStructure is AuditFileMasterFilesSupplier supplier)
        {
            SupplierCustomerId = supplier.SupplierID;
            AccountId = supplier.AccountID;
            OpeningBalance = (supplier.ItemElementName == ItemChoiceType2.OpeningDebitBalance ? supplier.Item : -supplier.Item).ToString("N2");
            ClosingBalance = (supplier.Item1ElementName == Item1ChoiceType2.ClosingDebitBalance ? supplier.Item1 : -supplier.Item1).ToString("N2");
        }
    }


    public EntityViewModel GetCopy()
    {
        var copy = (EntityViewModel)MemberwiseClone();
        return copy;
    }

    public override string ToString() => Title;
}
