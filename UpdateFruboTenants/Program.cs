using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace UpdateFruboTenants
{
    class Program
    {
        public static int _numberOfCharsToKeep = 15;
        static private string gitFolder = @"C:\Users\Mattias\Documents\GitHub\";
        static void Main(string[] args)
        {
            try
            {

                if (args != null && args.Length > 0)
                {
                    gitFolder = args[0];
                }
                //WriteSettings(gitFolder, new FruboLogin
                //{
                //    UserName = "",
                //    Password = ""
                //});
                //return;

                var login = ReadSettings(gitFolder);

                var apartments = new List<Apartment>();

                var cookieContainer = new CookieContainer();
                using (var handler = new HttpClientHandler() { CookieContainer = cookieContainer })
                {
                    using (HttpClient client = new HttpClient())
                    {
                        client.BaseAddress = new Uri("http://embedded.frubo.se");

                        if (!Login(client, login))
                        {
                            // TODO: We where unable to login, do something about this.
                            Console.WriteLine("Failed login!");
                            return;
                        }
                        Console.WriteLine("logged in!");

                        try
                        {
                            apartments.AddRange(GetApartmentsFromUrl(client));
                        }
                        catch (Exception ex)
                        {
                            //backgroundWorker1.ReportProgress(1, ex.ToString());
                        }

                        var folderBoard = gitFolder + "brfskagagard-styrelsen" + Path.DirectorySeparatorChar;
                        var folderBoardExists = Directory.Exists(folderBoard);
                        foreach (Apartment item in apartments)
                        {
                            //backgroundWorker1.ReportProgress(1, item.ToString());
                            var json = new DataContractJsonSerializer(typeof(Apartment));

                            var folder = gitFolder + "brfskagagard-lgh" + item.Number + Path.DirectorySeparatorChar;
                            var folderExists = Directory.Exists(folder);

                            // We only want to update repositories that we know about (read: that we have created)
                            if (folderExists)
                            {
                                using (
                                    var stream =
                                        File.Create(folder + "apartment.json"))
                                {
                                    json.WriteObject(stream, item);
                                    stream.Flush();
                                }
                            }

                            // We only want to update repositories that we know about (read: that we have created)
                            if (folderBoardExists)
                            {
                                using (
                                    var stream =
                                        File.Create(folderBoard + "apartment-" + item.Number + ".json"))
                                {
                                    json.WriteObject(stream, item);
                                    stream.Flush();
                                }
                            }


                        }

                        Console.WriteLine("Number of appartments: " + apartments.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                using (var stream = File.CreateText(gitFolder + "updatefrubotenants-last-error.txt"))
                {
                    stream.Write(ex.ToString());
                    stream.Flush();
                }
                throw;
            }
        }

        private static bool Login(HttpClient client, FruboLogin login)
        {
            var data = client.GetStringAsync("/").Result;

            var eventValidation = "";
            var viewState = "";
            var generator = "";
            if (!string.IsNullOrEmpty(data))
            {
                var match = Regex.Match(data, "id=\"__EVENTVALIDATION\" value=\"(?<test>[^\\\"]+)");
                if (match.Success)
                {
                    var group = match.Groups["test"];
                    if (group.Success)
                    {
                        eventValidation = group.Value;
                    }
                }

                match = Regex.Match(data, "id=\"__VIEWSTATE\" value=\"(?<test>[^\\\"]+)");
                if (match.Success)
                {
                    var group = match.Groups["test"];
                    if (group.Success)
                    {
                        viewState = group.Value;
                    }
                }

                match = Regex.Match(data, "id=\"__VIEWSTATEGENERATOR\" value=\"(?<test>[^\\\"]+)");
                if (match.Success)
                {
                    var group = match.Groups["test"];
                    if (group.Success)
                    {
                        generator = group.Value;
                    }
                }
            }

            SortedList<string, string> parameters = new SortedList<string, string>()
            {
                { "__EVENTTARGET", "" },
                { "__EVENTARGUMENT", "" },
                { "__LASTFOCUS", "" },
                { "__VIEWSTATE", viewState },
                { "__VIEWSTATEGENERATOR", "" },
                { "__EVENTVALIDATION", eventValidation },
                { "ctl00$ContentPlaceHolder1$txtUsername", login.UserName },
                { "ctl00$ContentPlaceHolder1$txtPassword", login.Password }
            };

            var response = client.PostAsync("/", new FormUrlEncodedContent(parameters)).Result;
            return response.IsSuccessStatusCode;
        }

        static private List<Apartment> GetApartmentsFromUrl(HttpClient client)
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

        private static List<Apartment> GetApartments(HttpClient client, string pdfUrl)
        {
            List<Apartment> apartments = new List<Apartment>();

            var pages = new StringBuilder();
            var pdfData = client.GetByteArrayAsync("/" + pdfUrl).Result;
            PdfReader reader = new PdfReader(pdfData);
            var nOfPages = reader.NumberOfPages;

            string pageHeader = null;
            for (int pageNumber = 1; pageNumber <= nOfPages; pageNumber++)
            {
                var pageText = ExtractTextFromPDFBytes(reader.GetPageContent(pageNumber));
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

        private static void AppendApartmentInfo(string apartmentData, string apartmentPrefix, Apartment apartment, int index)
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

        private static string GetBuilding(string apartmentData)
        {
            string building = null;
            //var buildingMatch = Regex.Match(apartmentData, "(?<building>[a-z0-9 ]+),", RegexOptions.IgnoreCase);
            var buildingMatch = Regex.Match(apartmentData, "(?<building>[\\w ]+),", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var buildingGroup = buildingMatch.Groups["building"];
            if (buildingMatch.Success && buildingGroup.Success)
            {
                building = buildingGroup.Value;
            }

            return building;
        }

        private static Owner[] GetOwners(string apartmentData)
        {
            var owners = new List<Owner>();
            var postAddressSufix = "KISTA";
            var postAddressIndex = apartmentData.IndexOf(postAddressSufix);
            if (postAddressIndex > 0)
            {
                var ownersData = apartmentData.Substring(postAddressIndex + postAddressSufix.Length);
                var ownersMatches = Regex.Matches(ownersData, "(?<share>[0-9]{2,3})(?<date>[0-9]{4}-[0-9]{2}-[0-9]{2})");
                var index = 0;
                foreach (Match match in ownersMatches)
                {
                    var shareGroup = match.Groups["share"];
                    var dateGroup = match.Groups["date"];

                    if (match.Success && shareGroup.Success && dateGroup.Success)
                    {
                        var owner = new Owner();
                        int share;
                        if (int.TryParse(shareGroup.Value, out share))
                        {
                            owner.Share = share;
                        }
                        owner.MovedIn = dateGroup.Value;
                        //var tmpName = ownersData.Substring(index, shareGroup.Index - (shareGroup.Length + index));
                        var tmpName = ownersData.Substring(index, shareGroup.Index - (index));
                        var name = Regex.Replace(tmpName, "[^\\w ]", "", RegexOptions.IgnoreCase | RegexOptions.IgnoreCase).Trim();
                        owner.Name = name;

                        // TODO: Make this changable by user (Should check against existing file and if tenant has changed, restore values).
                        owner.WayOfInfo = new string[] { "Brev" };
                        owner.Phone = "";   // TODO: If not set yet, get this from public records
                        owner.Email = "";

                        // TODO: Get this for public records
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

        #region ExtractTextFromPDFBytes
        /// <summary>
        /// This method processes an uncompressed Adobe (text) object 
        /// and extracts text.
        /// </summary>
        /// <param name="input">uncompressed</param>
        /// <returns></returns>
        public static string ExtractTextFromPDFBytes(byte[] input)
        {
            if (input == null || input.Length == 0) return "";

            try
            {
                string resultString = "";

                // Flag showing if we are we currently inside a text object
                bool inTextObject = false;

                // Flag showing if the next character is literal 
                // e.g. '\\' to get a '\' character or '\(' to get '('
                bool nextLiteral = false;

                // () Bracket nesting level. Text appears inside ()
                int bracketDepth = 0;

                // Keep previous chars to get extract numbers etc.:
                char[] previousCharacters = new char[_numberOfCharsToKeep];
                for (int j = 0; j < _numberOfCharsToKeep; j++) previousCharacters[j] = ' ';


                for (int i = 0; i < input.Length; i++)
                {
                    char c = (char)input[i];
                    if (input[i] == 213)
                        c = "'".ToCharArray()[0];

                    if (inTextObject)
                    {
                        // Position the text
                        if (bracketDepth == 0)
                        {
                            if (CheckToken(new string[] { "TD", "Td" }, previousCharacters))
                            {
                                resultString += "\n\r";
                            }
                            else
                            {
                                if (CheckToken(new string[] { "'", "T*", "\"" }, previousCharacters))
                                {
                                    resultString += "\n";
                                }
                                else
                                {
                                    if (CheckToken(new string[] { "Tj" }, previousCharacters))
                                    {
                                        resultString += " ";
                                    }
                                }
                            }
                        }

                        // End of a text object, also go to a new line.
                        if (bracketDepth == 0 &&
                            CheckToken(new string[] { "ET" }, previousCharacters))
                        {

                            inTextObject = false;
                            resultString += " ";
                        }
                        else
                        {
                            // Start outputting text
                            if ((c == '(') && (bracketDepth == 0) && (!nextLiteral))
                            {
                                bracketDepth = 1;
                            }
                            else
                            {
                                // Stop outputting text
                                if ((c == ')') && (bracketDepth == 1) && (!nextLiteral))
                                {
                                    bracketDepth = 0;
                                }
                                else
                                {
                                    // Just a normal text character:
                                    if (bracketDepth == 1)
                                    {
                                        // Only print out next character no matter what. 
                                        // Do not interpret.
                                        if (c == '\\' && !nextLiteral)
                                        {
                                            resultString += c.ToString();
                                            nextLiteral = true;
                                        }
                                        else
                                        {
                                            if (((c >= ' ') && (c <= '~')) ||
                                                ((c >= 128) && (c < 255)))
                                            {
                                                resultString += c.ToString();
                                            }

                                            nextLiteral = false;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // Store the recent characters for 
                    // when we have to go back for a checking
                    for (int j = 0; j < _numberOfCharsToKeep - 1; j++)
                    {
                        previousCharacters[j] = previousCharacters[j + 1];
                    }
                    previousCharacters[_numberOfCharsToKeep - 1] = c;

                    // Start of a text object
                    if (!inTextObject && CheckToken(new string[] { "BT" }, previousCharacters))
                    {
                        inTextObject = true;
                    }
                }

                return CleanupContent(resultString);
            }
            catch
            {
                return "";
            }
        }

        private static string CleanupContent(string text)
        {
            string[] patterns = { @"\\\(", @"\\\)", @"\\226", @"\\222", @"\\223", @"\\224", @"\\340", @"\\342", @"\\344", @"\\300", @"\\302", @"\\304", @"\\351", @"\\350", @"\\352", @"\\353", @"\\311", @"\\310", @"\\312", @"\\313", @"\\362", @"\\364", @"\\366", @"\\322", @"\\324", @"\\326", @"\\354", @"\\356", @"\\357", @"\\314", @"\\316", @"\\317", @"\\347", @"\\307", @"\\371", @"\\373", @"\\374", @"\\331", @"\\333", @"\\334", @"\\256", @"\\231", @"\\253", @"\\273", @"\\251", @"\\221" };
            string[] replace = { "(", ")", "-", "'", "\"", "\"", "à", "â", "ä", "À", "Â", "Ä", "é", "è", "ê", "ë", "É", "È", "Ê", "Ë", "ò", "ô", "ö", "Ò", "Ô", "Ö", "ì", "î", "ï", "Ì", "Î", "Ï", "ç", "Ç", "ù", "û", "ü", "Ù", "Û", "Ü", "®", "™", "«", "»", "©", "'" };

            for (int i = 0; i < patterns.Length; i++)
            {
                string regExPattern = patterns[i];
                Regex regex = new Regex(regExPattern, RegexOptions.IgnoreCase);
                text = regex.Replace(text, replace[i]);
            }

            return text;
        }

        #endregion

        #region CheckToken
        /// <summary>
        /// Check if a certain 2 character token just came along (e.g. BT)
        /// </summary>
        /// <param name="tokens">the searched token</param>
        /// <param name="recent">the recent character array</param>
        /// <returns></returns>
        private static bool CheckToken(string[] tokens, char[] recent)
        {
            foreach (string token in tokens)
            {
                if ((recent[_numberOfCharsToKeep - 3] == token[0]) &&
                    (recent[_numberOfCharsToKeep - 2] == token[1]) &&
                    ((recent[_numberOfCharsToKeep - 1] == ' ') ||
                    (recent[_numberOfCharsToKeep - 1] == 0x0d) ||
                    (recent[_numberOfCharsToKeep - 1] == 0x0a)) &&
                    ((recent[_numberOfCharsToKeep - 4] == ' ') ||
                    (recent[_numberOfCharsToKeep - 4] == 0x0d) ||
                    (recent[_numberOfCharsToKeep - 4] == 0x0a))
                    )
                {
                    return true;
                }
            }
            return false;
        }
        #endregion

        private static FruboLogin ReadSettings(string gitFolder)
        {
            var stream = System.IO.File.OpenRead(gitFolder + "frubo-setting.json");

            DataContractJsonSerializer serializer =
                new DataContractJsonSerializer(typeof(FruboLogin));

            var setting = serializer.ReadObject(stream) as FruboLogin;
            stream.Close();
            return setting;
        }

        private static void WriteSettings(string gitFolder, FruboLogin login)
        {
            var stream = System.IO.File.Create(gitFolder + "frubo-setting2.json");

            DataContractJsonSerializer serializer =
                new DataContractJsonSerializer(typeof(FruboLogin));

            serializer.WriteObject(stream, login);
            stream.Flush();
            stream.Close();
        }
    }
}
