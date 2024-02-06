using CommunityToolkit.Mvvm.ComponentModel;
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

namespace GneissBooks.ViewModels
{
    public partial class TaxClassViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _taxCode = "";

        [ObservableProperty]
        private string _standardTaxCode = "";

        [ObservableProperty]
        private string _description = "";

        [ObservableProperty]
        private decimal? _taxPercentage = 0m;

        public TaxClassViewModel()
        {
        }

        public TaxClassViewModel(AuditFileMasterFilesTaxTableEntry rawTaxClass) 
        {
            TaxCode = rawTaxClass.TaxCodeDetails?.FirstOrDefault()?.TaxCode ?? "";
            StandardTaxCode = rawTaxClass.TaxCodeDetails?.FirstOrDefault()?.StandardTaxCode ?? "";
            Description = rawTaxClass.TaxCodeDetails?.FirstOrDefault()?.Description ?? "";
            if (rawTaxClass.TaxCodeDetails?.FirstOrDefault()?.Item is decimal taxPercentage)
                TaxPercentage = taxPercentage;
        }
    }
}
