using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using GneissBooks.ViewModels;
using System;
using System.IO;
using System.Linq;

namespace GneissBooks.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    public async void LoadBooksButton_Click(object sender, RoutedEventArgs e)
    {
        var file = await TopLevel.GetTopLevel(this)!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions 
        { 
            Title = "Load books from file", 
            AllowMultiple = false,
            FileTypeFilter = new FilePickerFileType[] { new("SAF-T file") { Patterns = new[] { "*.xml" } } } 
        });

        if (file is not null && file.Count == 1)
        {
            var path = file.First().Path.LocalPath;

            (DataContext as MainViewModel)!.SaftFileImportPath = path;
            await (DataContext as MainViewModel)!.LoadSaftFile();
        }
    }

    public async void SaveBooksButton_Click(object sender, RoutedEventArgs e)
    {
        var file = await TopLevel.GetTopLevel(this)!.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions 
        { 
            Title = "Save/export books to file", 
            FileTypeChoices = new FilePickerFileType[] { new("SAF-T file") { Patterns = new[] { "*.xml" } } } 
        });

        if (file is not null && file.Path.LocalPath != "")
        {
            var path = file.Path.LocalPath;

            (DataContext as MainViewModel)!.SaftFileExportPath = path;
            await (DataContext as MainViewModel)!.SaveSaftFile();
        }
    }
    
    public async void ProcessSalesInvoiceButton_Click(object sender, RoutedEventArgs e)
    {
        var files = await TopLevel.GetTopLevel(this)!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions 
        { 
            Title = "Load and process sales invoice(s)", 
            AllowMultiple = true,
            FileTypeFilter = new FilePickerFileType[] { new("PDF invoice") { Patterns = new[] { "*.pdf" } } } 
        });

        if (files is not null && files.Count > 0)
        {
            var filePaths = Array.ConvertAll(files.ToArray(), (file) => file.Path.LocalPath);

            (DataContext as MainViewModel)!.InvoiceToProcessPaths = filePaths;
            await (DataContext as MainViewModel)!.ProcessSalesInvoices();
        }
    }
    
    public async void NewTransactionDocumentPathBrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var files = await TopLevel.GetTopLevel(this)!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions 
        { 
            Title = "Choose transaction document", 
            AllowMultiple = false,
            FileTypeFilter = new FilePickerFileType[] { 
                new("PDF files") { Patterns = new[] { "*.pdf" } }, 
                new("All files") { Patterns = new[] { "*" } } 
            } 
        });

        if (files is not null && files.Count == 1)
        {
            if ((DataContext as MainViewModel)!.NewManualTransaction is TransactionViewModel transactionViewModel)
            {
                transactionViewModel.DocumentPath = files.First().Path.LocalPath;
            }
        }
    }

    public async void ProcessPaymentButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new Window();
        window.Title = "Payment transaction wizard";
        window.Content = new PaymentProcessingView() { DataContext = new PaymentProcessingViewModel(this.DataContext as MainViewModel) };
        window.Width = 1200;
        window.Height = 700;
        window.Show();
    }
}
