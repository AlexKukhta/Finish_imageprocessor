using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Design;
using System.Drawing.Imaging;
using System.Globalization;
using System.Net;

namespace ImageProcessor
{
    class Program
    {
        private const string RenamedDirectory = "Renamed";
        private const string MarkedDirectory = "Marked";
        private const string SortDirectory = "SortedByGeocode";

        private const string WebReqPart1 = @"https://geocode-maps.yandex.ru/1.x/?geocode=";
        private const string WebReqPart2 = @"&kind=locality&results=1";

        private static readonly String[] imageExtensions = { ".JPG", ".jpg", ".jpeg", ".JPEG", ".png", ".gif", ".tiff", ".bmp", ".svg" };

        static void Main(string[] args)
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("Enter directory to process:");
                Console.WriteLine("Enter 1 to add date to files;");
                Console.WriteLine("Enter 2 to move and mark files;");
                Console.WriteLine("Enter 3 to sort files by geocode;");

                var sourceDirectoryPath = @"C:\Temp";

                Console.WriteLine("Select an action:");
                int.TryParse(Console.ReadLine(), out var selectedAction);

                switch (selectedAction)
                {
                    case 1:
                        AddDate(sourceDirectoryPath, RenamedDirectory);
                        break;
                    case 2:
                        MarkFiles(sourceDirectoryPath, MarkedDirectory);
                        break;
                    case 3:
                        SortImages(sourceDirectoryPath, SortDirectory);
                        break;
                    default:
                        Console.WriteLine("Select a valid action");
                        break;
                }

                Console.WriteLine("Continue?");

                var isRepeatRequired = Console.ReadLine();

                if (string.Equals(isRepeatRequired, "n"))
                {
                    break;
                }
            }

            Console.ReadLine();
        }

        private static void AddDate(string sourceDirectoryPath, string operationName)
        {
            var targetDirectoryPath = CreateTargetFolder(sourceDirectoryPath, operationName);
            var imageFilePaths = GetImageFiles(sourceDirectoryPath);

            foreach (var imageFilePath in imageFilePaths)
            {
                AddDateToFileName(imageFilePath, targetDirectoryPath);
            }
        }

        private static IEnumerable<string> GetImageFiles(string folderPath)
        {
            return Directory.GetFiles(folderPath).Where(itemPath => imageExtensions.Contains(Path.GetExtension(itemPath)));
        }

        private static void DrawDate(string sourceFilePath, string targetDirectoryName)
        {
            using (var fileStream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.ReadWrite))
            {
                using (var image = Image.FromStream(fileStream, false, false))
                {
                    var graphics = Graphics.FromImage(image);
                    var drawString = GetLastModifiedDate(sourceFilePath).ToString("MM-dd-yyyy HH mm ss fffffff");
                    var drawFont = new Font("Arial", 16);
                    var drawBrush = new SolidBrush(Color.Black);

                    // Create rectangle for drawing.
                    var width = 350.0F;
                    var height = 50.0F;
                    var x = image.Width - width - 10.0F;
                    var y = 75.0F;
                    var drawRect = new RectangleF(x, y, width, height);

                    // Draw string to screen.
                    graphics.DrawString(drawString, drawFont, drawBrush, drawRect);

                    var newPath = Path.Combine(targetDirectoryName, Path.GetFileName(sourceFilePath));

                    image.Save(newPath, image.RawFormat);
                }
            }
        }

        private static void MarkFiles(string sourceDirectoryPath, string operationName)
        {
            var targetDirectoryPath = CreateTargetFolder(sourceDirectoryPath, operationName);
            var imageFilePaths = GetImageFiles(sourceDirectoryPath);

            foreach (var imageFilePath in imageFilePaths)
            {
                DrawDate(imageFilePath, targetDirectoryPath);
            }
        }

        private static void AddDateToFileName(string filePath, string targetDirectoryPath)
        {
            var lastModifiedDate = GetLastModifiedDate(filePath).ToString("MM-dd-yyyy HH mm ss fffffff");

            File.Copy(filePath, Path.Combine(targetDirectoryPath, Path.GetFileNameWithoutExtension(filePath) + " " + lastModifiedDate + Path.GetExtension(filePath)), true);
        }

        private static string CreateTargetFolder(string sourceDirectoryPath, string operationName)
        {
            var directoryInfo = new DirectoryInfo(sourceDirectoryPath);
            var targetDirectoryPath = Path.Combine($"{directoryInfo.FullName}", $"{directoryInfo.Name}_{ operationName}");

            Directory.CreateDirectory(targetDirectoryPath);

            return targetDirectoryPath;
        }

        private static DateTime GetLastModifiedDate(string path)
        {
            return File.GetLastWriteTime(path);
        }

        private static void SortImages(string sourceDirectoryPath, string operationName)
        {
            var targetDirectoryPath = CreateTargetFolder(sourceDirectoryPath, operationName);
            var imageFilePaths = GetImageFiles(sourceDirectoryPath);
            List<string[]> listImgInfo = new List<string[]>();

            listImgInfo.Clear();
            foreach (var imageFilePath in imageFilePaths)
            {
                double? latitude = null;
                double? longitude = null;
                string[] imgInfo = new string[3];

                Bitmap bmp = new Bitmap(imageFilePath);

                foreach (PropertyItem propItem in bmp.PropertyItems)
                {
                    if ((propItem.Type == 5) && (propItem.Id == 2))   // широта
                        latitude = DecodeCoordinate(propItem);
                    if ((propItem.Type == 5) && (propItem.Id == 4))   // долгота
                        longitude = DecodeCoordinate(propItem);
                }

                if ((latitude.HasValue) && (longitude.HasValue))
                {
                    imgInfo[0] = imageFilePath;
                    imgInfo[1] = longitude.ToString().Replace(',', '.') + "," + latitude.ToString().Replace(',', '.');
                    imgInfo[2] = GetLocalityByGeocode(imgInfo[1]);

                    if (imgInfo[2].Length > 0)
                    {
                        listImgInfo.Add(imgInfo);
                    }
                    else
                        continue;
                }
                else
                {
                    Console.WriteLine("Файл: " + imageFilePath);
                    Console.WriteLine("Координаты не заданы!");
                }
                Console.WriteLine(string.Empty);
            }

            foreach (var imgInf in listImgInfo)
            {
                Console.WriteLine(imgInf[0] + "   " + imgInf[1] + "   " + imgInf[2]);
                if (!Directory.Exists(targetDirectoryPath + @"\" + imgInf[2]))
                    Directory.CreateDirectory(targetDirectoryPath + @"\" + imgInf[2]);

                File.Copy(imgInf[0], targetDirectoryPath + @"\" + imgInf[2] + @"\" + Path.GetFileName(imgInf[0]), true);
            }
        }

        private static double? DecodeCoordinate(PropertyItem propItem)
        {
            try
            {
                uint degreesNumerator = BitConverter.ToUInt32(propItem.Value, 0);
                uint degreesDenominator = BitConverter.ToUInt32(propItem.Value, 4);
                uint minutesNumerator = BitConverter.ToUInt32(propItem.Value, 8);
                uint minutesDenominator = BitConverter.ToUInt32(propItem.Value, 12);
                uint secondsNumerator = BitConverter.ToUInt32(propItem.Value, 16);
                uint secondsDenominator = BitConverter.ToUInt32(propItem.Value, 20);
                return (Convert.ToDouble(degreesNumerator) / Convert.ToDouble(degreesDenominator)) + (Convert.ToDouble(Convert.ToDouble(minutesNumerator) / Convert.ToDouble(minutesDenominator)) / 60) +
                       (Convert.ToDouble((Convert.ToDouble(secondsNumerator) / Convert.ToDouble(secondsDenominator)) / 3600));
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string GetLocalityByGeocode(string coordinates)
        {
            string locality = string.Empty;
            string temp = string.Empty;

            HttpWebRequest req = (HttpWebRequest)HttpWebRequest.Create(WebReqPart1 + coordinates + WebReqPart2);
            HttpWebResponse resp = (HttpWebResponse)req.GetResponse();

            using (StreamReader stream = new StreamReader(
                 resp.GetResponseStream(), Encoding.UTF8))
            {
                temp = stream.ReadToEnd();
            }

            temp = temp.Substring(temp.IndexOf(@"<kind>province</kind>"), temp.Length - temp.IndexOf(@"<kind>province</kind>"));
            temp = temp.Substring(temp.IndexOf(@"<kind>locality</kind>"), temp.Length - temp.IndexOf(@"<kind>locality</kind>"));
            temp = temp.Substring(temp.IndexOf("<name>"), temp.Length - temp.IndexOf("<name>"));
            temp = temp.Substring(6, temp.IndexOf(@"</name>") - 6);
            

            if (temp.Length > 0)
                locality = temp;

            return locality;
        }
    }
}
