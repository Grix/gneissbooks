using CommunityToolkit.Mvvm.ComponentModel;
using GneissBooks.Saft;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;

namespace GneissBooks.ViewModels
{
    public partial class TotalTaxAmountViewModel : ViewModelBase
    {
        public string TaxCode { get; }

        [ObservableProperty]
        private decimal _amount = 0m;

        public TotalTaxAmountViewModel(TaxInformationStructure saftInfo) 
        {
            TaxCode = saftInfo.TaxCode;
            Amount = saftInfo.TaxAmount.Amount;
        }

        public void AddFromSaftLine(TaxInformationStructure saftInfo)
        {
            if (saftInfo.TaxCode != TaxCode)
                Debug.WriteLine("WARNING: Existing tax code not matching added tx tax code: " + TaxCode + " " + saftInfo.TaxCode);

            Amount += saftInfo.TaxAmount.Amount;
        }

        public override string ToString()
        {
            return $"{TaxCode}: {Amount:N2}";
        }
    }
}
