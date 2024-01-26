using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace GneissBooks.Saft;

internal class SaftHelper
{
    /// <summary>
    /// Private constructor because we don't want/need people to instantiate this class as it only contains static helper functions.
    /// </summary>
    private SaftHelper() { }

    /// <summary>
    /// Deserializes Auditfile
    /// </summary>
    /// <param name="filePath">The path where the file will be read</param>
    /// <returns></returns>
    public static AuditFile? Deserialize(string filePath)
    {
        using (var reader = new StreamReader(filePath))
        {
            XmlSerializer xs = new XmlSerializer(typeof(AuditFile));
            return (AuditFile?)xs.Deserialize(reader);
        }
    }

    /// <summary>
    /// Serializes Auditfile
    /// </summary>
    /// <param name="auditfile">The auditfile object to serialize</param>
    /// <param name="filePath">The path where the file will be written</param>
    /// <param name="compression">Optional: When true file will be compressed into Zip file. Default: false</param>
    public static void Serialize(AuditFile auditfile, string filePath, bool compression = false)
    {
        var xmlFile = new FileInfo(filePath);
        var xml = new XmlSerializer(typeof(AuditFile));
        if (compression)
        {
            var zipFile = new FileInfo("SAF-T Export_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".zip");
            using (FileStream zipStream = zipFile.Open(FileMode.Create))
            {
                using (ZipArchive zip = new ZipArchive(zipStream, ZipArchiveMode.Create))
                {
                    ZipArchiveEntry entry = zip.CreateEntry(xmlFile.Name);
                    using (var writer = new StreamWriter(entry.Open()))
                    {
                        xml.Serialize(writer, auditfile);
                    }
                }
            }
        }
        else
        {
            using (TextWriter writer = new StreamWriter(xmlFile.Open(FileMode.Create)))
            {
                xml.Serialize(writer, auditfile);
            }
        }
    }

    /// <summary>
    /// Generates a filename string according to spec.
    /// </summary>
    /// <param name="organizationNumber">The organization number</param>
    /// <param name="currentFile">Optional: Current file of Multi-File export</param>
    /// <param name="totalFiles">Optional: Total files in Multi-File export</param>
    /// <returns>Generated filename string</returns>
    public static string MakeFilename(string organizationNumber, int currentFile = 1, int totalFiles = 1)
    {
        StringBuilder str = new StringBuilder();
        str.Append("SAF-T Financial_");                                         // Type of file
        str.Append(organizationNumber.Replace(" ", "") + "_");                  // Organization number/identifier, trimmed 
        string currentTime = DateTime.Now.ToString("yyyyMMddHHmmss");           // Date and Time in the format specified by SAF-T
        str.Append(currentTime);                                                // Append current time
        if (totalFiles > 1) str.Append("_" + currentFile + "_" + totalFiles);   // Current export file and total amount of export files (Multi file export)
        str.Append(".xml");
        return str.ToString();
    }

    /// <summary>
    /// Generates a filename string according to spec.
    /// </summary>
    /// <param name="auditfile">Auditfile object to get Organization Number from</param>
    /// <param name="currentFile">Optional: Current file of Multi-File export</param>
    /// <param name="totalFiles">Optional: Total files in Multi-File export</param>
    /// <returns>Generated filename string</returns>
    public static string MakeFilename(AuditFile auditfile, int currentFile = 1, int totalFiles = 1)
    {
        return MakeFilename(auditfile.Header.Company.RegistrationNumber, currentFile, totalFiles);
    }

    /// <summary>
    /// Generate AddressStructure  object from a string
    /// Example: STREETNAME 1, 1234 CITY, REGION
    /// </summary>
    /// <param name="str">Standard postal string, Example: STREETNAME 1, 1234 CITY, REGION</param>
    /// <param name="countrycode">Optional: Defaults to NO</param>
    /// <returns>AddressStructure  object generated from string</returns>
    public static AddressStructure PostalAddressFromString(string str, string countrycode = "NO") // STREETNAME 1, 1234 CITY, REGION
    {
        var arr = str.Split(',').Select((i) => { return i.Trim(); }).ToArray<string>();
        AddressStructure address = new AddressStructure();

        var numIndex = arr[0].LastIndexOf(" ");
        address.StreetName = arr[0].Substring(0, numIndex);
        address.Number = arr[0].Substring(numIndex, arr[0].Length - numIndex);

        numIndex = arr[1].IndexOf(" ");
        address.PostalCode = arr[1].Substring(0, numIndex);
        address.City = arr[1].Substring(numIndex, arr[1].Length - numIndex);

        address.Region = arr[2];

        address.Country = countrycode;

        return address;
    }
}



/*
NOTES on automatically generated schema SaftFinancialXmlSchema.cs:
Manual changes (must be repeated if re-generated):
In AmountStructure: 
- Added: [System.Xml.Serialization.XmlIgnoreAttribute()] public bool CurrencyAmountSpecified => ExchangeRateSpecified;
*/