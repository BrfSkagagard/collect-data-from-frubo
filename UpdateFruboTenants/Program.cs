using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.Globalization;
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
    public class Program
    {
        public static NumberFormatInfo NumberFormat = new NumberFormatInfo();
        public static int _numberOfCharsToKeep = 15;
        static private string gitFolder = @"C:\Users\Mattias\Documents\GitHub\";
        static void Main(string[] args)
        {
            NumberFormat = new NumberFormatInfo()
            {
                NumberDecimalSeparator = ","
            };

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
                var residuals = new List<ResidualInfo>();

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
                            apartments.AddRange(ApartmentRepository.GetApartmentsFromUrl(client));
                            residuals.AddRange(ResidualRepository.GetResidualListFromUrl(client));
                        }
                        catch (Exception ex)
                        {
                            //backgroundWorker1.ReportProgress(1, ex.ToString());
                        }

                        var jsonNotifications = new DataContractJsonSerializer(typeof(Notification[]));

                        var folderBoard = gitFolder + "brfskagagard-styrelsen" + Path.DirectorySeparatorChar;
                        var folderBoardExists = Directory.Exists(folderBoard);
                        foreach (Apartment item in apartments)
                        {
                            //backgroundWorker1.ReportProgress(1, item.ToString());
                            var json = new DataContractJsonSerializer(typeof(Apartment));
                            //var jsonNotifications = new DataContractJsonSerializer(typeof(Notification[]));

                            var folder = gitFolder + "brfskagagard-lgh" + item.Number + Path.DirectorySeparatorChar;
                            var folderExists = Directory.Exists(folder);

                            // We only want to update repositories that we know about (read: that we have created)
                            if (folderExists)
                            {
                                var fileName = folder + "apartment.json";
                                var fileExists = File.Exists(fileName);
                                // If file exist, check if owners has changed (If not, copy phone number, email and contact way)
                                if (fileExists)
                                {
                                    using (
                                        var stream =
                                            File.OpenRead(fileName))
                                    {
                                        var oldItem = json.ReadObject(stream) as Apartment;
                                        if (oldItem != null && oldItem.Owners != null && oldItem.Owners.Length > 0)
                                        {
                                            foreach (Owner owner in item.Owners)
                                            {
                                                var matchingOwner = oldItem.Owners.FirstOrDefault(o => o.Name == owner.Name);
                                                if (matchingOwner != null)
                                                {
                                                    owner.Email = matchingOwner.Email;
                                                    owner.Phone = matchingOwner.Phone;
                                                    owner.WayOfInfo = matchingOwner.WayOfInfo;
                                                    //owner.RegisteredAtAddress = matchingOwner.RegisteredAtAddress;
                                                }
                                            }
                                        }
                                    }
                                }

                                using (
                                    var stream =
                                        File.Create(fileName))
                                {
                                    json.WriteObject(stream, item);
                                    stream.Flush();
                                }

                                var residualFileName = folder + "notifications.json";
                                var residualsForApartment = residuals.Where(r => r.ApartmentNumber == item.Number).ToArray();
                                var hasResiduals = residualsForApartment != null && residualsForApartment.Length > 0;
                                if (hasResiduals)
                                {
                                    var notifications = new List<Notification>();
                                    foreach (var residual in residualsForApartment)
                                    {
                                        notifications.Add(new Notification
                                        {
                                            ApartmentNumber = residual.ApartmentNumber,
                                            Type = NotificationType.Critical,
                                            Message = Program.ToHtmlEncodedText($"Ni har en skuld till föreningen på {residual.Debt} kr. Vänligen kontakta Frubo för mer information."),
                                            ReadMoreLink = "http://www.brfskagagard.se/contact.html#ekonomi"
                                        });
                                    }

                                    using (
                                        var stream =
                                            File.Create(residualFileName))
                                    {
                                        jsonNotifications.WriteObject(stream, notifications.ToArray());
                                        stream.Flush();
                                    }
                                }
                                else
                                {
                                    var residualfileExists = File.Exists(residualFileName);
                                    if (residualfileExists)
                                    {
                                        File.Delete(residualFileName);
                                    }
                                }

                                //using (
                                //    var stream =
                                //        File.Create(fileName))
                                //{
                                //    json.WriteObject(stream, item);
                                //    stream.Flush();
                                //}

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

                        // We only want to update repositories that we know about (read: that we have created)
                        if (folderBoardExists)
                        {
                            var styrelsenNotifications = new List<Notification>();
                            if (residuals.Count > 0)
                            {
                                styrelsenNotifications.Add(new Notification
                                {
                                    ApartmentNumber = -1,
                                    Type = NotificationType.Warning,
                                    Message = Program.ToHtmlEncodedText("Det finns en eller flera poster på restlistan hos Frubo"),
                                    ReadMoreLink = "http://frubo.se/loginfrubo"
                                });
                            }

                            using (
                                var stream =
                                    File.Create(folderBoard + "notifications.json"))
                            {
                                jsonNotifications.WriteObject(stream, styrelsenNotifications.ToArray());
                                stream.Flush();
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

        public static string ToHtmlEncodedText(string text)
        {
            var regexp = "[^a-zA-Z0-9 \t]+";
            var output = Regex.Replace(text, regexp, new MatchEvaluator(MatchEvaluator));
            return output;
        }

        public static string MatchEvaluator(Match match)
        {
            return "&#" + string.Join(";&#", System.Text.Encoding.Default.GetBytes(match.Value)) + ";";
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
        public static bool CheckToken(string[] tokens, char[] recent)
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
