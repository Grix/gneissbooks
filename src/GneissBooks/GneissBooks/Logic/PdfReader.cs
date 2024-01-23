using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace GneissBooks;

internal class PdfReader
{
    public static string ExtractTextFromPdf(string path)
    {
        using (PdfDocument document = PdfDocument.Open(path))
        {
            StringBuilder text = new StringBuilder();

            for (int i = 0; i < document.NumberOfPages; i++)
            {
                Page page = document.GetPage(i + 1);
                text.AppendLine(page.Text);
            }

            return text.ToString();
        }
    }
}
