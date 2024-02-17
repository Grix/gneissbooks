using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using GneissBooks.ViewModels;
using System.Linq;

namespace GneissBooks;

public partial class EbayStatementProcessingView : UserControl
{
    public EbayStatementProcessingView()
    {
        InitializeComponent();
    }

    public async void SalesDocumentPathBrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var files = await TopLevel.GetTopLevel(this)!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose sales summary document",
            AllowMultiple = false,
            FileTypeFilter = new FilePickerFileType[] {
                new("PDF files") { Patterns = new[] { "*.pdf" } },
                new("All files") { Patterns = new[] { "*" } }
            }
        });

        if (files is not null && files.Count == 1)
        {
            (DataContext as EbayStatementProcessingViewModel)!.SalesDocumentPath = files.First().Path.LocalPath;
        }
    }

    public async void VatDocumentPathBrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var files = await TopLevel.GetTopLevel(this)!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose VAT/fee invoice document",
            AllowMultiple = false,
            FileTypeFilter = new FilePickerFileType[] {
                new("PDF files") { Patterns = new[] { "*.pdf" } },
                new("All files") { Patterns = new[] { "*" } }
            }
        });

        if (files is not null && files.Count == 1)
        {
            (DataContext as EbayStatementProcessingViewModel)!.VatDocumentPath = files.First().Path.LocalPath;
        }
    }
}