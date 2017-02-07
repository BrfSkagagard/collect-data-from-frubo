using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace UpdateFruboTenants
{
    public class ApartmentRepository
    {
        public static List<Apartment> GetApartmentsFromUrl(HttpClient client)
        {
            var apartments = new List<Apartment>();

            var data = client.GetStringAsync("/main.aspx?rubrikid=4").Result;
            if (!string.IsNullOrEmpty(data))
            {
                var match = Regex.Match(data, "(?<test>spararapport.aspx?[^\"]+)");
                var group = match.Groups["test"];
                if (match.Success && group.Success)
                {
                    apartments.AddRange(GetApartments(client, group.Value));
                }
            }
            return apartments;
        }

        public static List<Apartment> GetApartments(HttpClient client, string pdfUrl)
        {
            List<Apartment> apartments = new List<Apartment>();

            var pages = new StringBuilder();
            var pdfData = client.GetByteArrayAsync("/" + pdfUrl).Result;
            PdfReader reader = new PdfReader(pdfData);
            var nOfPages = reader.NumberOfPages;

            string pageHeader = null;
            for (int pageNumber = 1; pageNumber <= nOfPages; pageNumber++)
            {
                var pageText = Program.ExtractTextFromPDFBytes(reader.GetPageContent(pageNumber));
                var headerMatch = Regex.Match(pageText, "\n\r68\n");
                if (headerMatch.Success)
                {
                    if (pageHeader == null)
                    {
                        // save page header for later use
                        pageHeader = pageText.Substring(0, headerMatch.Index);
                    }
                    // exclude page header
                    pageText = pageText.Substring(headerMatch.Index);
                    pages.AppendLine(pageText);
                    Console.WriteLine(pageText);
                }
            }
            var strPages = pages.ToString();
            var apartmentPrefix = "122-01-";
            var matches = Regex.Matches(strPages, apartmentPrefix + "(?<nr>[0-9]{3})");
            Apartment prevApartment = null;
            int index = 0;
            foreach (Match match in matches)
            {
                var apartmentNumberGroup = match.Groups["nr"];
                if (match.Success && apartmentNumberGroup.Success)
                {
                    var apartment = new Apartment();
                    int nr;
                    if (int.TryParse(apartmentNumberGroup.Value, out nr))
                    {
                        if (prevApartment != null && prevApartment.Number == nr)
                        {
                            apartment = prevApartment;
                        }
                        apartment.Number = nr;
                        apartment.Size = GetApartmentSize(nr);
                        if (prevApartment != null)
                        {
                            var apartmentData = strPages.Substring(index, apartmentNumberGroup.Index - (index + apartmentPrefix.Length));
                            AppendApartmentInfo(apartmentData, apartmentPrefix, prevApartment, index);
                        }

                        index = apartmentNumberGroup.Index + apartmentNumberGroup.Length;
                        var isSameApartment = prevApartment == apartment;
                        prevApartment = apartment;
                        if (!isSameApartment)
                        {
                            apartments.Add(apartment);
                        }
                    }
                }
            }

            // Handle last apartment
            var lastApartmentData = strPages.Substring(index);
            AppendApartmentInfo(lastApartmentData, apartmentPrefix, prevApartment, index);

            return apartments;
        }

        public static void AppendApartmentInfo(string apartmentData, string apartmentPrefix, Apartment apartment, int index)
        {
            apartment.Building = GetBuilding(apartmentData);
            var owners = new List<Owner>();
            if (apartment.Owners != null)
            {
                owners.AddRange(apartment.Owners);
            }
            owners.AddRange(GetOwners(apartmentData));
            apartment.Owners = owners.ToArray();
        }

        public static string GetBuilding(string apartmentData)
        {
            string building = null;
            //var buildingMatch = Regex.Match(apartmentData, "(?<building>[a-z0-9 ]+),", RegexOptions.IgnoreCase);
            var buildingMatch = Regex.Match(apartmentData, "(?<building>[\\w ]+),", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var buildingGroup = buildingMatch.Groups["building"];
            if (buildingMatch.Success && buildingGroup.Success)
            {
                building = buildingGroup.Value;
                if (!string.IsNullOrEmpty(building))
                {
                    building = Program.ToHtmlEncodedText(building);
                }
            }

            return building;
        }

        public static Owner[] GetOwners(string apartmentData)
        {
            var owners = new List<Owner>();
            var postAddressSufix = "KISTA";
            var postAddressIndex = apartmentData.IndexOf(postAddressSufix);
            if (postAddressIndex > 0)
            {
                var ownersData = apartmentData.Substring(postAddressIndex + postAddressSufix.Length);
                var ownersMatches = Regex.Matches(ownersData, "(?<share>[0-9.]{2,4})(?<date>[0-9]{4}-[0-9]{2}-[0-9]{2})");
                var index = 0;
                foreach (Match match in ownersMatches)
                {
                    var shareGroup = match.Groups["share"];
                    var dateGroup = match.Groups["date"];

                    if (match.Success && shareGroup.Success && dateGroup.Success)
                    {
                        var owner = new Owner();
                        double share;
                        if (double.TryParse(shareGroup.Value, out share))
                        {
                            owner.Share = share;
                        }
                        owner.MovedIn = dateGroup.Value;
                        var tmpName = ownersData.Substring(index, shareGroup.Index - (index));
                        var name = Regex.Replace(tmpName, "[^\\w ]", "", RegexOptions.IgnoreCase | RegexOptions.IgnoreCase).Trim();
                        owner.Name = Program.ToHtmlEncodedText(name);

                        owner.WayOfInfo = new string[] { "Brev" };
                        owner.Phone = "";
                        owner.Email = "";
                        owner.RegisteredAtAddress = false;

                        owners.Add(owner);

                        index = dateGroup.Index + dateGroup.Length;
                    }
                }
            }
            return owners.ToArray();
        }

        public static int GetApartmentSize(int apartmentNumber)
        {
            switch (apartmentNumber)
            {
                case 732:
                case 742:
                case 752:
                case 762:
                    return 95;
                case 521:
                case 531:
                case 541:
                case 551:
                case 561:
                    return 88;
                case 721:
                case 722:
                case 731:
                case 741:
                case 751:
                case 761:
                    return 79;
                case 523:
                case 533:
                case 543:
                case 553:
                case 563:
                    return 67;
                case 723:
                case 733:
                case 743:
                case 753:
                case 763:
                    return 61;
                case 622:
                case 632:
                case 642:
                case 652:
                case 662:
                case 623:
                case 633:
                case 643:
                case 653:
                case 663:
                    return 60;
                case 532:
                case 542:
                case 552:
                case 562:
                    return 55;
                case 621:
                case 631:
                case 641:
                case 651:
                case 661:
                    return 45;
                case 624:
                case 634:
                case 644:
                case 654:
                case 664:
                    return 44;
                case 724:
                case 734:
                case 744:
                case 754:
                case 764:
                    return 43;
                default:
                    return 0;
            }
        }
    }
}
