using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace UpdateFruboTenants
{
    public class ResidualRepository
    {
        public static List<ResidualInfo> GetResidualListFromUrl(HttpClient client)
        {
            var residuals = new List<ResidualInfo>();

            var data = client.GetStringAsync("/main.aspx?rubrikid=1").Result;
            if (!string.IsNullOrEmpty(data))
            {
                var match = Regex.Match(data, "(?<test>spararapport.aspx\\?namn=\\~\\/Rapporter\\/rest[^\"]+)");
                var group = match.Groups["test"];
                if (match.Success && group.Success)
                {
                    residuals.AddRange(GetResiduals(client, group.Value));
                }
            }
            return residuals;
        }

        public static List<ResidualInfo> GetResiduals(HttpClient client, string pdfUrl)
        {
            List<ResidualInfo> residuals = new List<ResidualInfo>();

            var pages = new StringBuilder();
            var pdfData = client.GetByteArrayAsync("/" + pdfUrl).Result;
            PdfReader reader = new PdfReader(pdfData);
            var nOfPages = reader.NumberOfPages;

            for (int pageNumber = 1; pageNumber <= nOfPages; pageNumber++)
            {
                var pageText = Program.ExtractTextFromPDFBytes(reader.GetPageContent(pageNumber));
                pages.AppendLine(pageText);
            }
            var strPages = pages.ToString();

            var apartmentPrefix = "122-01-";
            var matches = Regex.Matches(strPages, apartmentPrefix + "(?<nr>[0-9]+)\\n\\r(?<amount>[0-9,]+)\\n\\r");
            foreach (Match match in matches)
            {
                var apartmentNumberGroup = match.Groups["nr"];
                var amountGroup = match.Groups["amount"];
                if (match.Success && apartmentNumberGroup.Success && amountGroup.Success)
                {
                    var residual = new ResidualInfo();
                    int nr;
                    double amount;

                    if (int.TryParse(apartmentNumberGroup.Value, out nr))
                    {
                        residual.ApartmentNumber = nr;
                    }

                    if (double.TryParse(amountGroup.Value, NumberStyles.Any, Program.NumberFormat, out amount))
                    {
                        residual.Debt = amount;
                    }

                    residuals.Add(residual);
                }
            }

            return residuals;
        }
    }
}
