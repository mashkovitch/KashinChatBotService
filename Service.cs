namespace KashinChatBotService
{
	using ImageProcessor;
	using ImageProcessor.Plugins.WebP.Imaging.Formats;
	using Newtonsoft.Json;
	using NLog;
	using OpenQA.Selenium;
	using OpenQA.Selenium.Chrome;
	using OpenQA.Selenium.Remote;
	using System;
	using System.Collections.Generic;
	using System.Configuration;
	using System.Diagnostics;
	using System.Drawing;
	using System.Drawing.Drawing2D;
	using System.Drawing.Imaging;
	using System.Drawing.Text;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Management;
	using System.Net;
	using System.Reflection;
	using System.Security.Cryptography;
	using System.Text;
	using System.Text.RegularExpressions;
	using System.Threading;
	using System.Web;
	using System.Xml;
	using Telegram.Bot;
	using Telegram.Bot.Args;
	using Telegram.Bot.Types;
	using Telegram.Bot.Types.Enums;
	using Telegram.Bot.Types.InputFiles;
	using Topshelf;
	using WikipediaNet;
	using WikipediaNet.Enums;
	using File = System.IO.File;

	static class Program
	{
		static int Main()
		{
			return (int)HostFactory.Run(x =>
			{
				x.SetServiceName("KASHINBOT");
				x.SetDisplayName("KASHINBOT");
				x.SetDescription("Бот Кашин чата");
				x.RunAsLocalSystem();
				x.StartAutomatically();
				x.Service<Service>();
				x.EnableServiceRecovery(r => r.RestartService(1));
			});
		}
	}

	class Service : ServiceControl
	{
		static readonly string FolderCurrent = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
		static readonly string BinaryLocation = Path.Combine(FolderCurrent, ConfigurationManager.AppSettings["BinaryLocation"]);
		static readonly string DriverLocation = Path.Combine(FolderCurrent, ConfigurationManager.AppSettings["DriverLocation"]);
		static Logger Log = null;
		static readonly string ConfigBotKey = ConfigurationManager.AppSettings["bot_key"];
		static readonly string folderCurrent = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
		static TelegramBotClient Bot = new TelegramBotClient(ConfigBotKey);
		static long obsceneCount = 0;
		static readonly SortedSet<string> obsceneCorpus = new SortedSet<string>(File.ReadAllLines(Path.Combine(folderCurrent, "obscene_corpus.txt")), StringComparer.OrdinalIgnoreCase);//https://github.com/odaykhovskaya/obscene_words_ru/blob/master/obscene_corpus.txt
																																														//static Regex rxObscene = new Regex(@"\w?(блад|бля|бляд|блят|говно|говна|ебал|ебат|ебись|збс|калоед|мудак|муден|пздц|пидор|пидр|пизд|пиздец|пиздос|пиздц|сука|хй|хуепут|хуи|хуисас|хуисос|хуй|ёб|ёба)\w?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
																																														//static readonly string lyrics = "Вот избран новый Президент\r\nСоединенных Штатов\r\nПоруган старый Президент\r\nСоединенных Штатов\r\n\r\nА нам-то что - ну, Президент\r\nНу, Съединенных Штатов\r\nА интересно все ж - Президент\r\nСоединенных Штатов";
		static readonly string[] videos = "DQACAgIAAxkDAAIEfl-hWGkIGlt9ox_aV5HCHoFzu6uLAAMJAAIKYQlJB2yyvqNHbAkeBA,DQACAgIAAxkDAAIEf1-hWG0NZKLp6QTGD-u-N0pB3C7dAAIBCQACCmEJSZZ3aBCNxEZiHgQ,DQACAgIAAxkDAAIEgF-hWG4yLT13doQC3ITCPosStLOOAAICCQACCmEJSTobDAOuB2-jHgQ,DQACAgIAAxkDAAIEgV-hWG4kS9Qd1On9gSa-oZ-ab3yXAAIDCQACCmEJSe51bd9iyaVEHgQ,DQACAgIAAxkDAAIEgl-hWG9D3WHMnhMTboOR6Ez_jd4XAAIECQACCmEJSX4HDOmNplicHgQ,DQACAgIAAxkDAAIEg1-hWHC4g6mPH7m1V6XwmEVCp1pAAAIFCQACCmEJSR_xnUupGljKHgQ".Split(',');
		//static readonly string[] videos = "DQACAgIAAx0ERyxeIgADRl-i-V6zBYdzjI3Uupfsx50xQA03AAKiCQACWuoZSfECZ7gcbXsMHgQ,DQACAgIAAx0ERyxeIgADR1-i-V_lu9svxRYNhOHlUIC_42MCAAKjCQACWuoZSZ2iGVAdo-MUHgQ,DQACAgIAAx0ERyxeIgADSF-i-V_BdH03xUOU8gcuDh6lv4xjAAKkCQACWuoZSZYDueGEI7UQHgQ,DQACAgIAAx0ERyxeIgADSV-i-WA_ZV7e4wv7LaGkEJ3ng7KlAAKlCQACWuoZSZVPCx5uUp7DHgQ,DQACAgIAAx0ERyxeIgADSl-i-WDfHjKJeb-fA66sLVrZImG5AAKmCQACWuoZScXohVBna-6PHgQ,DQACAgIAAx0ERyxeIgADS1-i-WEava3hiw8rRjlzF6ouF8RjAAKnCQACWuoZSYVU9mhWPr58HgQ,DQACAgIAAx0ERyxeIgADTF-i-WQXgdd192QKJ0TpufFBEz82AAKoCQACWuoZSdvAPxkM_IjeHgQ".Split(',');
		public bool Start(HostControl hostControl)
		{
			Log = GetLogger();
			//var ss = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
			//foreach (var file in Directory.EnumerateFiles(@"c:\Projects\KashinChatBotService\bin\Debug\!files", "*"))
			//	try
			//	{
			//		var json = JsonConvert.DeserializeXmlNode(File.ReadAllText(file), "root");
			//		var text = (json.DocumentElement.SelectSingleNode("//text") ?? json.DocumentElement.SelectSingleNode("//caption")).InnerText;
			//		foreach (XmlNode entity in json.DocumentElement.SelectNodes("//entities"))
			//			if (entity.SelectSingleNode("type").InnerText == "url")
			//				try { ss.Add(text.Substring(int.Parse(entity.SelectSingleNode("offset").InnerText), int.Parse(entity.SelectSingleNode("length").InnerText))); }
			//				catch { Console.WriteLine(entity.OuterXml); }
			//	}
			//	catch { Console.WriteLine(File.ReadAllText(file)); }

			ProcessMessages();
			return true;
		}

		private void ProcessMessages()
		{
			Console.WriteLine(Bot.GetMeAsync().Result);
			Bot.SetWebhookAsync("").Wait();
			Bot.OnUpdate += (object su, UpdateEventArgs evu) =>
			{
				try
				{
					//foreach (var file in Directory.EnumerateFiles(@"C:\Projects\KashinChatBotService\bin\Debug\!Movies", "*.mp4"))
					// using (var fs = File.OpenRead(file))
					//  Console.WriteLine(Bot.SendVideoNoteAsync(evu.Update.Message.Chat.Id, new InputTelegramFile(fs)).Result.VideoNote.FileId);
					try
					{
						var folder = Directory.CreateDirectory(Path.Combine(folderCurrent, "!Chats", evu.Update.Message.Chat.Id.ToString(), DateTime.Now.ToString("yyyyMMddHH"), evu.Update.Message.From.Id.ToString())).FullName;
						File.WriteAllText(Path.Combine(folder, evu.Update.Id + ".json"), JsonConvert.SerializeObject(evu.Update));
					}
					catch (Exception ex) { Log.Error(ex); }
					if (evu.Update.CallbackQuery != null || evu.Update.InlineQuery != null) // в этом блоке нам коллбэки и инлайны не нужны
						return;
					var message = evu.Update.Message;
					if (message == null)
						return;
					if (message.Type == MessageType.ChatMembersAdded)
					{
						Bot.SendStickerAsync(message.Chat.Id, new InputOnlineFile("CAACAgIAAxUAAV-gU5wEIiOUCtqfKySQKAVJJ--yAAJLCwACI7jfCGg5X0t4-mLoHgQ"), replyToMessageId: message.MessageId).Wait();
						return;
					}
					if (message.Type != MessageType.Text)
						return;
					if (evu.Update.Message.Entities != null)
					{
						var commands = evu.Update.Message.Entities.Where(entity => entity.Type == MessageEntityType.BotCommand).ToArray();
						if (commands.Length > 0)
						{
							var commandInfo = commands[0];
							var command = message.Text.Substring(commandInfo.Offset, commandInfo.Length).ToLower();
							var messageText = message.Text.Substring(commandInfo.Length).Trim();
							if ((command == "/sticker" || command == "/s") && message.Text.StartsWith("/s"))
							{
								using (var ms = new MemoryStream())
								{
									using (var img = Image.FromFile(CreateSticker(string.IsNullOrWhiteSpace(messageText) ? "РУССКИЕ\nВПЕРЕД!" : messageText)))
									using (var imageFactory = new ImageFactory(preserveExifData: false))
										imageFactory.Load(img).Format(new WebPFormat()).Quality(100).Save(ms);
									Bot.SendStickerAsync(message.Chat.Id, new InputOnlineFile(ms)).Wait();
								}
								return;
							}
							else if (command == "/wi" && message.Text.StartsWith("/wi"))
							{
								var wikipedia = new Wikipedia() { Language = Language.Russian, UseTLS = true, Limit = 1, What = What.Text };
								var results = wikipedia.Search(messageText);
								foreach (var s in results.Search)
								{
									var snippet = s.Snippet ?? "";
									try { snippet = Regex.Replace(Regex.Replace(s.Snippet, "<[^>]+>", " "), @"\s+", " "); }
									catch { }
									Bot.SendTextMessageAsync(message.Chat.Id, s.Title + "\r\n" + snippet + "\r\n" + s.Url, replyToMessageId: message.MessageId, disableWebPagePreview: true).Wait();
								}
								return;
							}
							else if (command == "/ss" && message.Text.StartsWith("/ss"))
							{
								if (evu.Update.Message.Entities != null)
									foreach (MessageEntity linkEntity in evu.Update.Message.Entities.Where(entity => entity.Type == MessageEntityType.Url))
										try
										{
											var link = new Uri(message.Text.Substring(linkEntity.Offset, linkEntity.Length));
											var host = link.Host.ToLower();
											if (host.Contains("twitter.com") || host.Contains("facebook.com") || host.Contains("fb.com") || host.Contains("instagram.com") || host.Contains("livejournal.com") || host.Contains("vk.com"))
											//|| host.Contains("t.me"))
											{
												KillProcess(BinaryLocation, null);
												KillProcess(DriverLocation, null);
												ChromeDriver driver = null;
												try
												{
													driver = new ChromeDriver(Path.GetDirectoryName(DriverLocation), ChromeOptionsBase, TimeSpan.FromMinutes(10));
													var shortLink = host.Contains("facebook.com") || host.Contains("fb.com") ? GetFBID(link.AbsoluteUri) : ShortenUrl(link.AbsoluteUri);
													var file = ScreenShooter.Get(shortLink, FolderCurrent, driver);
													using (var fs = File.OpenRead(file))
														Bot.SendPhotoAsync(message.Chat.Id, new InputOnlineFile(fs), host.Contains("facebook.com") || host.Contains("fb.com") ? GetFBID(link.AbsoluteUri) : link.AbsoluteUri, replyToMessageId: message.MessageId, disableNotification: true).Wait();
												}
												catch (Exception ex) { Log.Error(ex); }
												finally
												{
													try { driver.Close(); } catch { }
													try { driver.Quit(); } catch { }
												}
												return;
											}
										}
										catch (Exception ex) { Log.Error(ex); }
							}
						}
					}
					//var slotUrl = Regex.Replace(message.Text.Trim(), @"^/[^\s]+", string.Empty).Replace("\r", string.Empty).ToLower().Trim();
					//if (slotUrl.Contains("трамп") || slotUrl.Contains("байден") || slotUrl.Contains("баиден"))
					//    Bot.SendTextMessageAsync(message.Chat.Id, lyrics, replyToMessageId: message.MessageId).Wait();
					//else
					foreach (var word in Regex.Split(message.Text, @"\s+"))
						if (obsceneCorpus.Contains(word))
							Interlocked.Increment(ref obsceneCount);
					if (Interlocked.Read(ref obsceneCount) >= 100)
					{
						Bot.SendVideoNoteAsync(message.Chat.Id, new InputOnlineFile(videos.OrderBy(s => Guid.NewGuid()).First()), replyToMessageId: message.MessageId).Wait();
						Interlocked.Exchange(ref obsceneCount, 0);
					}
					if (evu.Update.Message.Entities != null)
						foreach (MessageEntity linkEntity in evu.Update.Message.Entities.Where(entity => entity.Type == MessageEntityType.Url))
							try
							{
								var link = new Uri(message.Text.Substring(linkEntity.Offset, linkEntity.Length));
								var host = link.Host.ToLower();
								if (host.Contains("twitter.com") || host.Contains("facebook.com") || host.Contains("fb.com") || host.Contains("instagram.com") || host.Contains("livejournal.com") || host.Contains("vk.com"))
								//|| host.Contains("t.me"))
								{
									KillProcess(BinaryLocation, null);
									KillProcess(DriverLocation, null);
									ChromeDriver driver = null;
									try
									{
										driver = new ChromeDriver(Path.GetDirectoryName(DriverLocation), ChromeOptionsBase, TimeSpan.FromMinutes(10));
										var shortLink = host.Contains("facebook.com") || host.Contains("fb.com") ? GetFBID(link.AbsoluteUri) : ShortenUrl(link.AbsoluteUri);
										var file = ScreenShooter.Get(shortLink, FolderCurrent, driver);
										using (var fs = File.OpenRead(file))
											Bot.SendPhotoAsync(message.Chat.Id, new InputOnlineFile(fs), host.Contains("facebook.com") || host.Contains("fb.com") ? GetFBID(link.AbsoluteUri) : link.AbsoluteUri, replyToMessageId: message.MessageId, disableNotification: true).Wait();
									}
									catch (Exception ex) { Log.Error(ex); }
									finally
									{
										try { driver.Close(); } catch { }
										try { driver.Quit(); } catch { }
									}
									return;
								}
							}
							catch (Exception ex) { Log.Error(ex); }
				}
				catch (Exception ex)
				{
					Log.Error(ex);
				}
			};
			// запускаем прием обновлений
			Bot.StartReceiving();
		}

		public bool Stop(HostControl hostControl)
		{
			Bot.StopReceiving();
			return true;
		}

		static string CreateSticker(string messageText)
		{
			var slotUrl = messageText;
			var foo = new PrivateFontCollection();
			foo.AddFontFile(Path.Combine(folderCurrent, "FreeSet-ExtraBoldOblique.otf"));
			var fontSize = 72f;
			var font = new Font((FontFamily)foo.Families[0], fontSize);
			SizeF labelMeasure;
			//SizeF labelMeasureStable;
			using (Bitmap result = new Bitmap(512, 512, PixelFormat.Format24bppRgb))
			{
				result.SetResolution(96f, 96f);
				using (Graphics graphics = Graphics.FromImage(result))
				{
					graphics.Clear(Color.Transparent);
					graphics.CompositingQuality = CompositingQuality.HighQuality;
					graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
					graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
					graphics.SmoothingMode = SmoothingMode.HighQuality;
					graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
					font = new Font((FontFamily)foo.Families[0], fontSize);
					labelMeasure = graphics.MeasureString(slotUrl, font);
				}
			}
			var backColor = ColorTranslator.FromHtml("#e0312c");// FE95A3
			var rowsCount = Regex.Split(slotUrl, @"\r?\n").Length;
			var angle = 10;
			var b = (int)Math.Abs(((90 - angle) * Math.Tan(angle))) * rowsCount;
			using (var result = new Bitmap((int)labelMeasure.Width + b * 2, (int)labelMeasure.Height + (rowsCount * 20), PixelFormat.Format24bppRgb))
			{
				result.MakeTransparent();
				result.SetResolution(96f, 96f);
				using (var graphics = Graphics.FromImage(result))
				{
					var brushSolid = new SolidBrush(backColor);// new TextureBrush(img);// 
					graphics.Clear(Color.Transparent);
					graphics.CompositingQuality = CompositingQuality.HighQuality;
					graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
					graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
					graphics.SmoothingMode = SmoothingMode.HighQuality;
					graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
					graphics.FillPolygon(brushSolid, new[] { new Point(b, 0), new Point(b, result.Height), new Point(0, result.Height), new Point(b, 0) });
					graphics.FillPolygon(brushSolid, new[] { new Point(result.Width - 1, 0), new Point(result.Width - b, result.Height), new Point(result.Width - b, 0), new Point(result.Width - 1, 0) });
					graphics.FillRectangle(brushSolid, b, 0, result.Width - (b * 2), result.Height);
					var offset = 10;
					var barWidth = result.Width - b;
					foreach (var line in slotUrl.Split('\n'))
					{
						var text = line;
						var strikeout = false;
						var leftAlign = false;
						if (line.StartsWith(@"\s") || line.StartsWith(@"\l"))
						{
							strikeout = line.StartsWith(@"\s");
							leftAlign = line.StartsWith(@"\l");
							text = line.Substring(2);
						}
						var m = graphics.MeasureString(text, font);
						var textLeft = b;
						if (!leftAlign)
							textLeft += (int)((barWidth - m.Width) - (barWidth - m.Width) / 2) - (rowsCount + 1) * 20;
						graphics.DrawString(text, font, new SolidBrush(Color.White), new PointF(textLeft, offset));
						if (strikeout)
						{
							var height = 25;
							var width = 10;
							var top = (int)(offset + m.Height / 2);// - height / 4);
							var left = (int)(textLeft);
							var right = (int)(m.Width + 20 + (barWidth - m.Width) - (barWidth - m.Width) / 2);
							var brush = new SolidBrush(Color.White);
							graphics.FillPolygon(brush, new[] { new Point(left + width, top), new Point(left + width, top + height), new Point(left, top + height), new Point(left + width, top) });
							graphics.DrawLine(new Pen(brush, height), left + width, top + height / 2, right, top);
							graphics.FillPolygon(brush, new[] { new Point(right, top - height / 2), new Point(right + width, top - height / 2), new Point(right, top + height - height / 2), new Point(right, top - height / 2) });
						}
						offset += (int)m.Height + 5;
					}
				}

				using (var ms = new MemoryStream(ImageResize.Resize(result, null, 512, null, null, null, false)))
				using (var iii = Image.FromStream(ms))
				using (Bitmap r = new Bitmap(512, iii.Height, PixelFormat.Format24bppRgb))
				{
					r.MakeTransparent();
					r.SetResolution(96f, 96f);
					using (Graphics graphics = Graphics.FromImage(r))
					{
						graphics.Clear(Color.Transparent);
						graphics.CompositingQuality = CompositingQuality.HighQuality;
						graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
						graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
						graphics.SmoothingMode = SmoothingMode.HighQuality;
						graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
						graphics.DrawImage(iii, r.Width - iii.Width, 0, r.Width, r.Height);
					}
					var fileResult = Path.Combine(folderCurrent, DateTime.Now.Ticks.ToString() + ".png");
					File.WriteAllBytes(fileResult, ImageResize.SaveAs(r, ImageFormat.Png, 100));
					return fileResult;
				}
			}
		}

		static Logger GetLogger()
		{
			var logFileName = "NLog.config";
			if (!File.Exists(logFileName))
				File.WriteAllText(logFileName, @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<nlog xmlns=""http://www.nlog-project.org/schemas/NLog.xsd""
      xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
      xsi:schemaLocation=""http://www.nlog-project.org/schemas/NLog.xsd NLog.xsd""
      autoReload=""true"">
	<targets>
		<target	xsi:type=""File"" name=""file_log""
			fileName=""${basedir}/Logs/${shortdate}.log""
			layout=""${longdate} | ${uppercase:${level}} | ${windows-identity} | ${message}"" />
	</targets>
	<rules>
		<logger name=""*"" minlevel=""Error"" writeTo=""file_log"" />
		<!-- Trace,Debug,Info,Warn,Error,Fatal    -->
	</rules>
</nlog>");
			return LogManager.GetCurrentClassLogger();
		}

		static string GetFBID(string linkUri)
		{
			try
			{
				var fbid = HttpUtility.ParseQueryString(new Uri(linkUri).Query)["fbid"];
				if (String.IsNullOrWhiteSpace(fbid))
					fbid = linkUri.TrimEnd('/').Split('/').Last();
				long id;
				if (!long.TryParse(fbid ?? string.Empty, out id))
					try
					{
						fbid = Regex.Matches(linkUri, @"\d{14,}")[0].Value;
						fbid = long.TryParse(fbid ?? string.Empty, out id) ? id.ToString() : string.Empty;
					}
					catch { }
				return String.IsNullOrWhiteSpace(fbid) ? linkUri : ("https://fb.com/" + fbid);
			}
			catch { return linkUri; }
		}

		private static ChromeOptions ChromeOptionsBase
		{
			get
			{
				var options = new ChromeOptions();
				options.BinaryLocation = BinaryLocation;
				//if (!Environment.UserInteractive)
				options.AddArgument("headless");
				options.BinaryLocation = BinaryLocation;
				options.AddArgument("no-default-browser-check");
				options.AddArgument("no-first-run");
				options.AddArgument("incognito");
				options.AddArgument("start-maximized");
				options.AddArgument("disable-infobars");
				options.AddArgument("silent");
				options.AddArgument("lang=ru");
				options.AddArgument("enable-precise-memory-info");
				options.AddArgument("disable-plugins");
				options.AddArgument("disable-default-apps");
				options.AddArgument("disable-extensions");
				options.AddArgument("disable-gpu");
				options.AddArgument("no-sandbox");
				options.AddArgument("window-size=1920,1080");
				//options.AddUserProfilePreference("plugins.always_open_pdf_externally", true);
				//options.AddUserProfilePreference("plugins.plugins_disabled", new String[] { "Adobe Flash Player", "Chrome PDF Viewer" });
				//options.AddUserProfilePreference("profile.default_content_settings.popups", 0);
				//options.EnableMobileEmulation(new ChromeMobileEmulationDeviceSettings() { Width = 600, Height = 800, UserAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 6_0 like Mac OS X) AppleWebKit/536.26 (KHTML, like Gecko) Version/6.0 Mobile/10A5376e Safari/8536.25" });
				//options.AddArgument("disable-images");
				//options.AddUserProfilePreference("profile.managed_default_content_settings.images", 2);
				//options.AddUserProfilePreference("download.prompt_for_download", "false");
				//options.AddUserProfilePreference("download.directory_upgrade", "true");
				//options.AddUserProfilePreference("download.default_directory", DownloadsFolder);
				//options.AddUserProfilePreference("profile.default_content_setting_values.automatic_downloads", 1);
				//options.AddUserProfilePreference("profile.content_settings.exceptions.automatic_downloads.*.setting", 1);
				return options;

			}
		}

		static string ShortenUrl(string url)
		{
			try
			{
				return new WebClient().DownloadString("https://clck.ru/--?url=" + HttpUtility.UrlEncode(url)).Trim();
			}
			catch (Exception ex)
			{
				Log.Error(ex);
				Thread.Sleep(1000);
				try
				{
					return new WebClient().DownloadString("https://clck.ru/--?url=" + HttpUtility.UrlEncode(url)).Trim();
				}
				catch (Exception eex) { Log.Error(eex); }
			}
			return url;
		}
		public static void KillProcess(string ExecutablePath, string CommandLine)
		{
			int? pid;
			while ((pid = FindProcess(ExecutablePath, CommandLine)).HasValue)
				try
				{
					Process.GetProcessById(pid.Value).Kill();
				}
				catch (Exception ex)
				{
					Log.Error(new Exception(ex.Message + "\tPID\t" + pid + "\t" + ExecutablePath + "\t" + CommandLine));
				}
		}

		public static int? FindProcess(string ExecutablePath, string CommandLine)
		{
			using (var searcher = new ManagementObjectSearcher("SELECT ExecutablePath, CommandLine, ProcessId FROM Win32_Process WHERE ExecutablePath <> '' AND CommandLine <> ''"))
			using (var objects = searcher.Get())
				foreach (var obj in objects.Cast<ManagementBaseObject>())
					try
					{
						var exePath = (obj.Properties["ExecutablePath"].Value ?? "").ToString();
						if (String.IsNullOrWhiteSpace(exePath))
							continue;
						var cmdLine = (obj.Properties["CommandLine"].Value ?? "").ToString();
						if (!String.IsNullOrWhiteSpace(CommandLine) && String.IsNullOrWhiteSpace(cmdLine))
							continue;
						if (exePath.ToUpper().Trim() == ExecutablePath.ToUpper().Trim() && (string.IsNullOrWhiteSpace(CommandLine) || cmdLine.EndsWith(CommandLine)))
							return int.Parse(obj["ProcessId"]?.ToString());
					}
					catch (Exception ex)
					{
						Log.Error(new Exception(ex.Message + "\t" + ExecutablePath + "\t" + CommandLine));
					}
			return null;
		}

	}

	public class ScreenShooter
	{
		public static string Get(string shortLink, string folder, ChromeDriver driver)
		{
			string driverUrl;
			var fileImage = Path.Combine(folder, ComputeHash(shortLink) + ".png");
			if (File.Exists(fileImage) && new FileInfo(fileImage).Length > 0)
				return fileImage;
			int width = 0, height = 0, x = 0, y = 0;
			try
			{
				if (driver == null)
					throw new Exception("ChromeDriver is null!");
				driver.Navigate().GoToUrl(shortLink + (shortLink.ToLower().Contains("livejournal.com") ? "?embed=1" : string.Empty));
				while (String.IsNullOrWhiteSpace(driver.PageSource))
					Thread.Sleep(100);

				try
				{
					int repeat = 300;
					while (!((IJavaScriptExecutor)driver).ExecuteScript("return document.readyState").Equals("complete") && repeat-- > 0)
						Thread.Sleep(100);
				}
				catch (Exception) { }
				try { driver.FindElement(By.TagName("body")).SendKeys(Keys.F11); }
				catch { }
				driverUrl = driver.Url.ToLower();
				if (driverUrl.Contains("livejournal.com"))
					try { driver.Manage().Window.Size = new Size(500, driver.Manage().Window.Size.Height); }
					catch { }
				else
					driver.Manage().Window.Maximize();
				if (driverUrl.Contains("facebook.com") && driverUrl.Contains("/videos/"))
				{
					driver.Navigate().GoToUrl(driverUrl.Replace("/videos/", "/posts/"));
					while (String.IsNullOrWhiteSpace(driver.PageSource))
						Thread.Sleep(100);
					driverUrl = driver.Url.ToLower();
				}
				var driverJS = (IJavaScriptExecutor)driver;
				var driverSS = (ITakesScreenshot)driver;
				var jsHide = "arguments[0].style.visibility='hidden'";
				var jsDrop = "arguments[0].style.display='none'";
				//var jsBorder = "arguments[0].style.border='solid 1px red'";
				var jsScroll = "arguments[0].scrollIntoView(true);";

				try { driver.ExecuteScript("arguments[0].style.display='none'", driver.FindElement(By.TagName("header"))); } catch { }
				try
				{
					driver.ExecuteScript(@"
						var elems = window.document.getElementsByTagName('*');
						for(i = 0; i < elems.length; i++) 
						{ 
								if (window.getComputedStyle) 
								{
									 var elemStyle = window.getComputedStyle(elems[i], null); 
									 if (elemStyle.getPropertyValue('position') == 'fixed' && elems[i].innerHTML.length != 0 )
										 elems[i].parentNode.removeChild(elems[i]);
								}
								else 
								{
									 var elemStyle = elems[i].currentStyle; 
									 if (elemStyle.position == 'fixed' && elems[i].childNodes.length != 0)
										 elems[i].parentNode.removeChild(elems[i]); 
								}   
						}");
				}
				catch { }
				if (driverUrl.Contains("/t.me/"))
				{
					driver.Navigate().GoToUrl(driverUrl + "?embed=1");
					IWebElement elem = driver.FindElement(By.XPath("//*[contains(@class,'tgme_widget_message')]"));
					driverJS.ExecuteScript(jsScroll, elem);
					width = elem.Size.Width;
					height = elem.Size.Height;
					x = ((RemoteWebElement)elem).LocationOnScreenOnceScrolledIntoView.X;
					y = ((RemoteWebElement)elem).LocationOnScreenOnceScrolledIntoView.Y;
				}
				else if (driverUrl.Contains("facebook.com"))
				{
					try { driverJS.ExecuteScript(jsDrop, driver.FindElement(By.Id("pagelet_navigation"))); } catch { }
					try { driverJS.ExecuteScript(jsHide, driver.FindElement(By.XPath("//a[@aria-label='Story options']"))); } catch { }
					try { driverJS.ExecuteScript(jsDrop, driver.FindElement(By.Id("u_0_c"))); } catch { }
					try { driverJS.ExecuteScript(jsHide, driver.FindElement(By.Id("headerArea"))); } catch { }
					try { driverJS.ExecuteScript(jsHide, driver.FindElement(By.XPath("//a[text()='Follow']"))); } catch { }
					var elem = driver.FindElement(By.CssSelector("div.userContent")).FindElement(By.XPath(".."));
					driverJS.ExecuteScript(jsScroll, elem);
					width = elem.Size.Width;
					height = elem.Size.Height + 25;
					x = ((RemoteWebElement)elem).LocationOnScreenOnceScrolledIntoView.X;
					y = ((RemoteWebElement)elem).LocationOnScreenOnceScrolledIntoView.Y;
				}
				else if (driverUrl.Contains("vk.com"))
				{

					try
					{
						foreach (var elm in driver.FindElements(By.XPath("//span[contains(@class,'rel_date_needs_update')]")))
						{
							var pubDate = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc)
								.AddSeconds(long.Parse(elm.GetAttribute("time"))).ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss") + " (MSK)";
							driverJS.ExecuteScript("arguments[0].innerHTML='" + pubDate + "';", elm);
						}
					}
					catch (Exception ex)
					{
						Console.WriteLine(ex);
					}
					IWebElement element1 = null;
					try { element1 = driver.FindElement(By.CssSelector("div.post_header")); }
					catch (Exception ex)
					{
						try { File.WriteAllText(Path.ChangeExtension(fileImage, ".htm"), driver.PageSource); } catch { }
						try { driverSS.GetScreenshot().SaveAsFile(fileImage + ".jpg", ScreenshotImageFormat.Png); } catch { }
						throw new Exception("div.post_header" + "\t" + shortLink + "\t" + ex.Message);
					}
					IWebElement element2 = driver.FindElement(By.CssSelector("div.wall_text"));
					try { driverJS.ExecuteScript(jsHide, driver.FindElement(By.ClassName("uiScaledImageContainer")).FindElement(By.XPath("../.."))); }
					catch { }
					width = Math.Max(element1.Size.Width, element2.Size.Width) - 30;
					height = element1.Size.Height + element2.Size.Height;
					height = height < driver.Manage().Window.Size.Height ? height : driver.Manage().Window.Size.Height - element1.Location.Y;
					x = element1.Location.X + 15;
					y = element1.Location.Y + 15;
				}
				else if (driverUrl.Contains("instagram.com"))
				{
					try { driverJS.ExecuteScript(jsHide, driver.FindElement(By.XPath("//nav"))); } catch { }
					IWebElement elem = driver.FindElement(By.XPath("//article"));
					driverJS.ExecuteScript(jsScroll, elem);
					try
					{
						foreach (var elm in elem.FindElements(By.XPath("//time[@datetime]")))
							try
							{
								var pubDate = DateTime.ParseExact(elm.GetAttribute("datetime"), "yyyy-MM-ddTHH:mm:ss.000Z", CultureInfo.InvariantCulture)
									.ToLocalTime()
									.ToString("dd.MM.yyyy HH:mm:ss") + " (MSK)";
								driverJS.ExecuteScript("arguments[0].innerHTML='" + pubDate + "';", elm);
								Console.WriteLine(pubDate);
							}
							catch { }
					}
					catch { }
					width = elem.Size.Width;
					height = elem.Size.Height;
					x = ((RemoteWebElement)elem).LocationOnScreenOnceScrolledIntoView.X;
					y = ((RemoteWebElement)elem).LocationOnScreenOnceScrolledIntoView.Y;
				}
				else if (driverUrl.Contains("livejournal.com"))
				{
					if (!driverUrl.Contains("?embed"))
					{
						driver.Navigate().GoToUrl(driverUrl + "?embed=1");
						while (String.IsNullOrWhiteSpace(driver.PageSource))
							Thread.Sleep(100);
					}
					try { driverJS.ExecuteScript(jsHide, driver.FindElement(By.CssSelector("div.share-embed-footer__pane"))); } catch { }
					try { driverJS.ExecuteScript(jsHide, driver.FindElement(By.CssSelector("div.share-embed-header__wrap-btn"))); } catch { }
					var element1 = driver.FindElement(By.XPath("//header"));
					var element2 = driver.FindElement(By.XPath("//article"));
					driverJS.ExecuteScript(jsScroll, element1);
					width = Math.Max(element1.Size.Width, element2.Size.Width);
					height = element1.Size.Height + element2.Size.Height;
					x = element1.Location.X;
					y = element1.Location.Y;
				}
				else if (driverUrl.Contains("twitter.com"))
				{
					Thread.Sleep(3000);
					try
					{
						foreach (var elm in driver.FindElements(By.XPath("//*[text()='View' or text()='Посмотреть']")))
							driverJS.ExecuteScript("arguments[0].click();", elm);
					}
					catch { }
					try { driverJS.ExecuteScript("arguments[0].remove();", new object[] { driver.FindElement(By.XPath("//*[@data-testid='primaryColumn']/div/div[1]")) }); } catch { }
					try { driverJS.ExecuteScript("arguments[0].remove();", new object[] { driver.FindElement(By.Id("layers")) }); } catch { }
					try { driverJS.ExecuteScript("arguments[0].remove();", new object[] { driver.FindElement(By.XPath("//*[@role='group']")) }); } catch { }
					try { driverJS.ExecuteScript("arguments[0].remove();", new object[] { driver.FindElement(By.Name("svg")) }); } catch { }
					try { driverJS.ExecuteScript("arguments[0].remove();", new object[] { driver.FindElement(By.XPath("//*[@data-testid='caret']")) }); } catch { }
					try { driverJS.ExecuteScript("arguments[0].remove();", new object[] { driver.FindElement(By.XPath("//*[@role='button']")) }); } catch { }
					Thread.Sleep(3000);
					IWebElement elem = null;
					try
					{
						elem = driver.FindElement(By.XPath("//div[@role='main' or contains(@class,'original-permalink-page')]"));
					}
					catch
					{
						try
						{
							elem = driver.FindElement(By.XPath("//div[contains(@class,'permalink-tweet')]"));
						}
						catch
						{
							try { elem = driver.FindElement(By.CssSelector("div.permalink-tweet-container")); }
							catch
							{
								try { elem = driver.FindElement(By.XPath(".//div[@data-testid='tweetDetail']")); }
								catch { elem = driver.FindElement(By.XPath("//article")); }
							}
						}
					}
					try { driverJS.ExecuteScript("arguments[0].remove();", new object[] { elem.FindElement(By.XPath(".//*[@class='css-1dbjc4n'][3]")) }); } catch { }
					try
					{
						foreach (var elm in elem.FindElements(By.XPath(".//div[contains(@class,'js-machine-translated-tweet-container') or contains(@class,'stream-item-footer') or contains(@class,'tweet-stats-container')]")))// or contains(@class,'stream-footer')
							driverJS.ExecuteScript(jsDrop, elm);
					}
					catch { }
					try
					{
						foreach (var elm in elem.FindElements(By.XPath("//span[contains(@class,'_timestamp')]")))
						{
							var ts = long.Parse(elm.GetAttribute("data-time"));
							var pubDate = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc).AddSeconds(ts).ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss") + " (MSK)";
							driverJS.ExecuteScript("arguments[0].innerHTML='" + pubDate + "';", elm);
						}
					}
					catch (Exception ex)
					{
						Console.WriteLine(ex);
					}
					try
					{
						foreach (var elm in elem.FindElements(By.XPath("//div[contains(@class,'ProfileTweet-action')]")))
							driverJS.ExecuteScript(jsHide, elm);
					}
					catch { }
					try
					{
						foreach (var elm in elem.FindElements(By.XPath("//div[@class='dropdown']")))
							driverJS.ExecuteScript(jsHide, elm);
					}
					catch { }
					try
					{
						foreach (var elm in elem.FindElements(By.XPath("//button[contains(@class,'Tombstone-action')]")))
							driverJS.ExecuteScript("arguments[0].click();", elm);
					}
					catch { }
					try
					{
						foreach (var elm in elem.FindElements(By.XPath(".//div[contains(@class,'tweet-stats-container') or @id='descendants']")))
							driverJS.ExecuteScript(jsDrop, elm);

					}
					catch { }
					try
					{
						foreach (var elm in elem.FindElements(By.XPath("//span[contains(@class,'_timestamp')]")))
						{
							var ts = long.Parse(elm.GetAttribute("data-time"));
							var pubDate = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc).AddSeconds(ts).ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss") + " (MSK)";
							driverJS.ExecuteScript("arguments[0].innerHTML='" + pubDate + "';", elm);
							Console.WriteLine(pubDate);
						}
					}
					catch (Exception ex)
					{
						Console.WriteLine(ex);
					}
					//driverJS.ExecuteScript(jsScroll, elem);
					//убираем рамку и скругленные края
					width = elem.Size.Width - 10;
					height = elem.Size.Height - 10;
					x = elem.Location.X + 5;
					y = elem.Location.Y + 5;
					//try
					//{
					//    var e = driver.FindElement(By.CssSelector("div.permalink-tweet-container"));
					//    driverJS.ExecuteScript(jsBorder, e);
					//    height = e.Size.Height - 10;
					//    x = elem.Location.X + 5;
					//    y = elem.Location.Y + 5;
					//}
					//catch (Exception ex)
					//{
					//    string s = ex.Message;
					//}
				}
				Directory.CreateDirectory(folder);
				try { File.WriteAllBytes(fileImage, SaveAs(GetEntireScreenshot(driver), ImageFormat.Png, 100)); }
				catch (Exception ex)
				{
					LogManager.GetCurrentClassLogger().Error(ex);
					Thread.Sleep(2000);
					try { File.WriteAllBytes(fileImage, SaveAs(GetEntireScreenshot(driver), ImageFormat.Png, 100)); }
					catch { Thread.Sleep(2000); driverSS.GetScreenshot().SaveAsFile(fileImage, ScreenshotImageFormat.Png); }
				}
			}
			finally
			{
				try { driver.Close(); } catch { }
				try { driver.Quit(); } catch { }
			}
			File.WriteAllBytes(fileImage, SaveAs(Crop(fileImage, width, height, x, y, shortLink), ImageFormat.Png, 100));
			return fileImage;
		}



		static Bitmap GetEntireScreenshot(ChromeDriver browser)
		{
			var browserJSE = (IJavaScriptExecutor)browser;
			var element = browser.FindElement(By.XPath("//body"));
			var elementSize = element.Size;
			var elementLctn = element.Location;
			int totalWidth = (int)(long)browserJSE.ExecuteScript("return document.body.offsetWidth");
			int elemWidth = elementSize.Width;
			int totalHeight = (int)(long)browserJSE.ExecuteScript("return  document.body.parentNode.scrollHeight");
			int elemHeight = elementSize.Height;
			int viewportWidth = (int)(long)browserJSE.ExecuteScript("return document.body.clientWidth");
			int viewportHeight = (int)(long)browserJSE.ExecuteScript("return window.innerHeight");
			List<Rectangle> rectangles = new List<Rectangle>();
			for (int i = 0; i < Math.Min(totalHeight, viewportHeight * 2); i += viewportHeight)
			{
				if (i > 0)
				{
					try { browser.ExecuteScript("arguments[0].style.display='none'", browser.FindElement(By.TagName("header"))); } catch { }
					try
					{
						browser.ExecuteScript(@"
						var elems = window.document.getElementsByTagName('*');
						for(i = 0; i < elems.length; i++) 
						{ 
								if (window.getComputedStyle) 
								{
									 var elemStyle = window.getComputedStyle(elems[i], null); 
									 if (elemStyle.getPropertyValue('position') == 'fixed' && elems[i].innerHTML.length != 0 )
										 elems[i].parentNode.removeChild(elems[i]);
								}
								else 
								{
									 var elemStyle = elems[i].currentStyle; 
									 if (elemStyle.position == 'fixed' && elems[i].childNodes.length != 0)
										 elems[i].parentNode.removeChild(elems[i]); 
								}   
						}");
					}
					catch { }
				}
				int newHeight = viewportHeight;
				if (i + viewportHeight > totalHeight)
					newHeight = totalHeight - i;
				for (int ii = 0; ii < totalWidth; ii += viewportWidth)
				{
					int newWidth = viewportWidth;
					if (ii + viewportWidth > totalWidth)
						newWidth = totalWidth - ii;
					rectangles.Add(new Rectangle(ii, i, newWidth, newHeight));
				}
			}
			using (var result = new Bitmap(totalWidth, Math.Min(totalHeight, viewportHeight * 2), PixelFormat.Format24bppRgb))
			{
				result.SetResolution(96f, 96f);
				using (Graphics graphics = Graphics.FromImage(result))
				{
					graphics.Clear(Color.Transparent);
					graphics.CompositingQuality = CompositingQuality.HighQuality;
					graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
					graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
					graphics.SmoothingMode = SmoothingMode.HighQuality;
					Rectangle previous = Rectangle.Empty;
					foreach (var rectangle in rectangles)
					{
						if (previous != Rectangle.Empty)
						{
							int xDiff = rectangle.Right - previous.Right;
							int yDiff = rectangle.Bottom - previous.Bottom;
							browserJSE.ExecuteScript(String.Format("window.scrollBy({0}, {1})", xDiff, yDiff));
							Thread.Sleep(200);
						}
						Image screenshotImage;
						using (MemoryStream memStream = new MemoryStream(((ITakesScreenshot)browser).GetScreenshot().AsByteArray))
							screenshotImage = Image.FromStream(memStream);
						Rectangle sourceRectangle = new Rectangle(viewportWidth - rectangle.Width, viewportHeight - rectangle.Height, rectangle.Width, rectangle.Height);
						graphics.DrawImage(screenshotImage, rectangle, sourceRectangle, GraphicsUnit.Pixel);
						previous = rectangle;
					}
				}
				return (Bitmap)result.Clone();
			}
		}
		public static string Get(ChromeDriver driver, string shortLink, string fileImage)
		{
			if (File.Exists(fileImage) && new FileInfo(fileImage).Length > 0)
				return fileImage;
			var driverUrl = driver.Url;
			int width = 0, height = 0, x = 0, y = 0;
			try
			{
				int repeat = 300;
				while (!((IJavaScriptExecutor)driver).ExecuteScript("return document.readyState").Equals("complete") && repeat-- > 0)
					Thread.Sleep(100);
			}
			catch (Exception) { }
			try { driver.FindElement(By.TagName("body")).SendKeys(Keys.F11); }
			catch { }
			Thread.Sleep(8000);
			driverUrl = driver.Url.ToLower();
			if (driverUrl.Contains("livejournal.com"))
				try { driver.Manage().Window.Size = new Size(500, driver.Manage().Window.Size.Height); }
				catch { }
			else
				try { driver.Manage().Window.Maximize(); }
				catch { }
			if (driverUrl.Contains("facebook.com") && driverUrl.Contains("/videos/"))
			{
				driver.Navigate().GoToUrl(driverUrl.Replace("/videos/", "/posts/"));
				while (String.IsNullOrWhiteSpace(driver.PageSource))
					Thread.Sleep(100);
				driverUrl = driver.Url.ToLower();
			}
			var driverJS = (IJavaScriptExecutor)driver;
			var driverSS = (ITakesScreenshot)driver;
			var jsHide = "arguments[0].style.visibility='hidden'";
			var jsDrop = "arguments[0].style.display='none'";
			//var jsBorder = "arguments[0].style.border='solid 1px red'";
			var jsScroll = "arguments[0].scrollIntoView(true);";
			if (driverUrl.Contains("/t.me/"))
			{
				driver.Navigate().GoToUrl(driverUrl + "?embed=1");
				IWebElement elem = driver.FindElement(By.XPath("//div[@id='widget_message']"));
				driverJS.ExecuteScript(jsScroll, elem);
				width = elem.Size.Width;
				height = elem.Size.Height;
				x = ((RemoteWebElement)elem).LocationOnScreenOnceScrolledIntoView.X;
				y = ((RemoteWebElement)elem).LocationOnScreenOnceScrolledIntoView.Y;
			}
			else if (driverUrl.Contains("facebook.com") || driverUrl.Contains("fb.com"))
			{
				try { driverJS.ExecuteScript(jsHide, driver.FindElement(By.XPath("//a[@aria-label='Story options']"))); } catch { }
				try { driverJS.ExecuteScript(jsDrop, driver.FindElement(By.Id("u_0_c"))); } catch { }
				try { driverJS.ExecuteScript(jsHide, driver.FindElement(By.Id("headerArea"))); } catch { }
				try { driverJS.ExecuteScript(jsHide, driver.FindElement(By.XPath("//a[text()='Follow']"))); } catch { }
				var elem = driver.FindElement(By.CssSelector("div.userContent")).FindElement(By.XPath(".."));
				driverJS.ExecuteScript(jsScroll, elem);
				width = elem.Size.Width;
				height = elem.Size.Height;
				x = ((RemoteWebElement)elem).LocationOnScreenOnceScrolledIntoView.X;
				y = ((RemoteWebElement)elem).LocationOnScreenOnceScrolledIntoView.Y;
			}
			else if (driverUrl.Contains("vk.com"))
			{
				try
				{
					foreach (var elm in driver.FindElements(By.XPath("//span[contains(@class,'rel_date_needs_update')]")))
					{
						var pubDate = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc)
							.AddSeconds(long.Parse(elm.GetAttribute("time"))).ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss") + " (MSK)";
						driverJS.ExecuteScript("arguments[0].innerHTML='" + pubDate + "';", elm);
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex);
				}
				try
				{
					foreach (var elm in driver.FindElements(By.XPath("//*[contains(@class,'feature_tooltip__close')]")))
						driverJS.ExecuteScript("arguments[0].click();", elm);
				}
				catch { }
				try
				{
					foreach (var elm in driver.FindElements(By.XPath("//*[contains(@class,'feature_info_tooltip')]")))
						driverJS.ExecuteScript(jsHide, elm);
				}
				catch { }
				try { driverJS.ExecuteScript(jsHide, driver.FindElement(By.ClassName("uiScaledImageContainer")).FindElement(By.XPath("../.."))); }
				catch { }

				IWebElement element1 = driver.FindElement(By.CssSelector("div.post_header"));
				IWebElement element2 = driver.FindElement(By.CssSelector("div.wall_text"));
				try { driverJS.ExecuteScript(jsHide, driver.FindElement(By.ClassName("uiScaledImageContainer")).FindElement(By.XPath("../.."))); }
				catch { }
				width = Math.Max(element1.Size.Width, element2.Size.Width) - 30;
				height = element1.Size.Height + element2.Size.Height;
				height = height < driver.Manage().Window.Size.Height ? height : driver.Manage().Window.Size.Height - element1.Location.Y;
				x = element1.Location.X + 15;
				y = element1.Location.Y + 15;
			}
			else if (driverUrl.Contains("instagram.com"))
			{
				try { driverJS.ExecuteScript(jsHide, driver.FindElement(By.XPath("//nav"))); } catch { }
				IWebElement elem = driver.FindElement(By.XPath("//article"));
				driverJS.ExecuteScript(jsScroll, elem);
				try
				{
					foreach (var elm in elem.FindElements(By.XPath("//time[@datetime]")))
						try
						{
							var pubDate = DateTime.ParseExact(elm.GetAttribute("datetime"), "yyyy-MM-ddTHH:mm:ss.000Z", CultureInfo.InvariantCulture)
								.ToLocalTime()
								.ToString("dd.MM.yyyy HH:mm:ss") + " (MSK)";
							driverJS.ExecuteScript("arguments[0].innerHTML='" + pubDate + "';", elm);
							Console.WriteLine(pubDate);
						}
						catch { }
				}
				catch { }
				width = elem.Size.Width;
				height = elem.Size.Height;
				x = ((RemoteWebElement)elem).LocationOnScreenOnceScrolledIntoView.X;
				y = ((RemoteWebElement)elem).LocationOnScreenOnceScrolledIntoView.Y;
			}
			else if (driverUrl.Contains("livejournal.com"))
			{
				if (!driverUrl.Contains("?embed"))
				{
					driver.Navigate().GoToUrl(driverUrl + "?embed=1");
					while (String.IsNullOrWhiteSpace(driver.PageSource))
						Thread.Sleep(100);
				}
				try { driverJS.ExecuteScript(jsHide, driver.FindElement(By.CssSelector("div.share-embed-footer__pane"))); } catch { }
				try { driverJS.ExecuteScript(jsHide, driver.FindElement(By.CssSelector("div.share-embed-header__wrap-btn"))); } catch { }
				var element1 = driver.FindElement(By.XPath("//header"));
				var element2 = driver.FindElement(By.XPath("//article"));
				driverJS.ExecuteScript(jsScroll, element1);
				width = Math.Max(element1.Size.Width, element2.Size.Width);
				height = element1.Size.Height + element2.Size.Height;
				x = element1.Location.X;
				y = element1.Location.Y;
			}
			else if (driverUrl.Contains("twitter.com"))
			{
				Thread.Sleep(8000);
				try { driverJS.ExecuteScript(jsHide, driver.FindElement(By.CssSelector("div.follow-bar"))); } catch { }
				try { driverJS.ExecuteScript(jsHide, driver.FindElement(By.CssSelector("div.ProfileTweet-action--more"))); } catch { }
				IWebElement elem = null;
				try
				{
					elem = driver.FindElement(By.XPath("//div[@role='main' or contains(@class,'original-permalink-page')]"));
				}
				catch
				{
					try
					{
						elem = driver.FindElement(By.XPath("//div[contains(@class,'permalink-tweet')]"));
					}
					catch
					{
						try { elem = driver.FindElement(By.CssSelector("div.permalink-tweet-container")); }
						catch { elem = driver.FindElement(By.XPath(".//div[@data-testid='tweetDetail']")); }
					}
				}
				try { driverJS.ExecuteScript("arguments[0].style.display='none'", elem.FindElement(By.XPath(".//div[@aria-label='Tweet actions']"))); } catch { }
				try { driverJS.ExecuteScript("arguments[0].style.display='none'", elem.FindElement(By.XPath(".//div[text()='Likes']")).FindElement(By.XPath("../../../.."))); } catch { }
				try
				{
					foreach (var elm in elem.FindElements(By.XPath(".//div[contains(@class,'js-machine-translated-tweet-container') or contains(@class,'stream-item-footer') or contains(@class,'tweet-stats-container')]")))// or contains(@class,'stream-footer')
						driverJS.ExecuteScript(jsDrop, elm);
				}
				catch { }
				try
				{
					foreach (var elm in elem.FindElements(By.XPath("//span[contains(@class,'_timestamp')]")))
					{
						var ts = long.Parse(elm.GetAttribute("data-time"));
						var pubDate = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc).AddSeconds(ts).ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss") + " (MSK)";
						driverJS.ExecuteScript("arguments[0].innerHTML='" + pubDate + "';", elm);
						Console.WriteLine(pubDate);
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex);
				}
				try
				{
					foreach (var elm in elem.FindElements(By.XPath("//div[contains(@class,'ProfileTweet-action')]")))
						driverJS.ExecuteScript(jsHide, elm);
				}
				catch { }
				try
				{
					foreach (var elm in elem.FindElements(By.XPath("//div[@class='dropdown']")))
						driverJS.ExecuteScript(jsHide, elm);
				}
				catch { }
				try
				{
					foreach (var elm in elem.FindElements(By.XPath("//button[contains(@class,'Tombstone-action')]")))
						driverJS.ExecuteScript("arguments[0].click();", elm);
				}
				catch { }
				try
				{
					foreach (var elm in elem.FindElements(By.XPath(".//div[contains(@class,'tweet-stats-container') or @id='descendants']")))
						driverJS.ExecuteScript(jsDrop, elm);

				}
				catch { }
				try
				{
					foreach (var elm in elem.FindElements(By.XPath("//span[contains(@class,'_timestamp')]")))
					{
						var ts = long.Parse(elm.GetAttribute("data-time"));
						var pubDate = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc).AddSeconds(ts).ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss") + " (MSK)";
						driverJS.ExecuteScript("arguments[0].innerHTML='" + pubDate + "';", elm);
						Console.WriteLine(pubDate);
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex);
				}
				//driverJS.ExecuteScript(jsScroll, elem);
				//убираем рамку и скругленные края
				width = elem.Size.Width - 10;
				height = elem.Size.Height - 10;
				x = elem.Location.X + 5;
				y = elem.Location.Y + 5;
				//try
				//{
				//    var e = driver.FindElement(By.CssSelector("div.permalink-tweet-container"));
				//    driverJS.ExecuteScript(jsBorder, e);
				//    height = e.Size.Height - 10;
				//    x = elem.Location.X + 5;
				//    y = elem.Location.Y + 5;
				//}
				//catch (Exception ex)
				//{
				//    string s = ex.Message;
				//}
			}
			Directory.CreateDirectory(Path.GetDirectoryName(fileImage));
			try { driverSS.GetScreenshot().SaveAsFile(fileImage, ScreenshotImageFormat.Png); }
			catch
			{
				Thread.Sleep(2000);
				try { driverSS.GetScreenshot().SaveAsFile(fileImage, ScreenshotImageFormat.Png); }
				catch (Exception ex) { throw new Exception(fileImage + ", " + ex.Message); }
			}

			File.WriteAllBytes(fileImage, SaveAs(Crop(fileImage, width, height, x, y, shortLink), ImageFormat.Png, 100));
			return fileImage;
		}

		public static Image Crop(string fileImage, int width, int height, int x, int y, string link)
		{
			return Crop(File.ReadAllBytes(fileImage), width, height, x, y, link);
		}

		public static Image Crop(byte[] fileData, int width, int height, int x, int y, string link)
		{
			using (var ms = new MemoryStream(fileData))
			using (Image image = Image.FromStream(ms))
			{
				x = x >= 0 ? x : 0;
				y = y >= 0 ? y : 0;
				width = Math.Min(image.Width, width);
				height = height <= 0 ? image.Height : Math.Min(image.Height, height);
				if (width <= 0)
					width = 200;
				var brushText = new SolidBrush(Color.White);
				var brushBack = new SolidBrush(Color.Silver);
				var dateText = link.Replace("https://", string.Empty).Replace("http://", string.Empty).Replace("www.", string.Empty) + " | t.me/SnapItBot";//, " + DateTime.Now.ToString("dd.MM.yy HH:mm") + "(MSK)
				float fontSize = 8;
				var font = new Font(new FontFamily("Lucida Console"), fontSize, FontStyle.Regular, GraphicsUnit.Point);
				Rectangle rectDest = new Rectangle(0, 0, width, height);
				Rectangle rectSrc = new Rectangle(x, y, width, height);
				Rectangle rectLabel;
				Image finalPic;
				SizeF textSize;
				try
				{
					using (Bitmap result = new Bitmap(width, height, image.PixelFormat))
					{
						result.SetResolution(image.HorizontalResolution, image.VerticalResolution);
						using (Graphics gfx = Graphics.FromImage(result))
						{
							gfx.Clear(Color.Transparent);
							textSize = gfx.MeasureString(dateText, font);
							gfx.FillRectangle(Brushes.White, 0, 0, result.Width, result.Height);
							gfx.SmoothingMode = SmoothingMode.None;
							gfx.CompositingQuality = CompositingQuality.HighQuality;
							gfx.InterpolationMode = InterpolationMode.HighQualityBicubic;
							gfx.PixelOffsetMode = PixelOffsetMode.HighQuality;
							gfx.DrawImage(image, rectDest, rectSrc, GraphicsUnit.Pixel);
						}
						finalPic = (Image)result.Clone();
					}
				}
				catch (Exception ex)
				{
					throw new Exception(ex.Message + "\r\n" + "width: " + width + ", height: " + height);
				}
				rectLabel = new Rectangle(
					finalPic.Width - (int)Math.Ceiling(textSize.Width),
					finalPic.Height - (int)Math.Ceiling(textSize.Height),
					(int)Math.Ceiling(textSize.Width),
					(int)Math.Ceiling(textSize.Height)
				);

				var marginHeight = Math.Max(20, (int)Math.Ceiling(textSize.Height));
				var marginWidth = Math.Max(20, (int)Math.Ceiling(textSize.Height));

				//if (CountDiffColoredPixelsRectangle((Bitmap)image, rectLabel, Color.White) == 0)
				//	marginHeight = 0;

				using (finalPic)
				using (Bitmap result = new Bitmap(width + marginWidth, height + marginHeight, image.PixelFormat))
				{
					result.SetResolution(image.HorizontalResolution, image.VerticalResolution);
					using (Graphics gfx = Graphics.FromImage(result))
					{
						gfx.FillRectangle(Brushes.White, 0, 0, result.Width, result.Height);
						gfx.SmoothingMode = SmoothingMode.None;
						gfx.CompositingQuality = CompositingQuality.HighQuality;
						gfx.InterpolationMode = InterpolationMode.HighQualityBicubic;
						gfx.PixelOffsetMode = PixelOffsetMode.HighQuality;
						gfx.DrawImage(finalPic, marginWidth / 2, marginHeight / 2);
						var textX = finalPic.Width - (int)Math.Ceiling(textSize.Width) + marginWidth / 2;
						var textY = marginHeight == 0
							? finalPic.Height - (int)Math.Ceiling(textSize.Height)
							: finalPic.Height + marginHeight / 2;
						gfx.FillRectangle(brushBack, textX, textY, (int)Math.Ceiling(textSize.Width), (int)Math.Ceiling(textSize.Height));
						gfx.DrawString(dateText, font, brushText, textX, textY);
					}
					return (Image)result.Clone();
				}
			}
		}

		public static byte[] SaveAs(Image image, ImageFormat imageFormat, Int64 quality)
		{
			ImageCodecInfo[] imageCodecs = ImageCodecInfo.GetImageDecoders();
			ImageCodecInfo formatEncoder = Array.Find<ImageCodecInfo>(imageCodecs
				, (c => c.FormatID == imageFormat.Guid));
			if (formatEncoder == null)
				formatEncoder = Array.Find<ImageCodecInfo>(imageCodecs
					, (c => c.FormatID == ImageFormat.Jpeg.Guid));
			using (var qualityEncoderParameter = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality))
			using (var encoderParameters = new EncoderParameters(1))
			using (var ms = new MemoryStream())
			{
				encoderParameters.Param[0] = qualityEncoderParameter;
				image.Save(ms, formatEncoder, encoderParameters);
				return ms.ToArray();
			}
		}

		public static void TrimWhitespace(Bitmap main, ref int width, ref int height, ref int cropX, ref int cropY)
		{
			int? startX = null;
			int? startY = null;
			int? endX = null;
			int? endY = null;

			int widthFull = Math.Min(main.Width, width + cropX);
			int heightFull = Math.Min(main.Height, height + cropY);
			int stride;
			int bytesPerPixel;
			byte[] data;
			BitmapData bmMainData = main.LockBits(new Rectangle(0, 0, main.Width, main.Height), ImageLockMode.ReadOnly, main.PixelFormat);
			stride = bmMainData.Stride;
			bytesPerPixel = Math.Abs(stride) / main.Width;
			data = new byte[Math.Abs(stride) * main.Height];
			System.Runtime.InteropServices.Marshal.Copy(bmMainData.Scan0, data, 0, data.Length);
			main.UnlockBits(bmMainData);

			var mark = Color.White;
			var colorWhite = MyColor.FromARGB(mark.A, mark.R, mark.G, mark.B);

			var counter = 0;
			for (int x = cropX; x < widthFull && !startX.HasValue; ++x)
				if ((counter = CountDiffColoredPixelsColumn(data, stride, bytesPerPixel, x, cropY, heightFull, colorWhite)) > 0)
					startX = x;

			for (int y = cropY; y < heightFull && !startY.HasValue; ++y)
				if ((counter = CountDiffColoredPixelsRow(data, stride, bytesPerPixel, y, cropX, widthFull, colorWhite)) > 0)
					startY = y;

			for (int x = widthFull - 1; x > startX && !endX.HasValue; --x)
				if ((counter = CountDiffColoredPixelsColumn(data, stride, bytesPerPixel, x, cropY, heightFull, colorWhite)) > 0)
					endX = x;

			for (int y = heightFull - 1; y > startY && !endY.HasValue; --y)
				if ((counter = CountDiffColoredPixelsRow(data, stride, bytesPerPixel, y, cropX, widthFull, colorWhite)) > 0)
					endY = y + 5;

			cropX = startX.HasValue ? startX.Value : cropX;
			cropY = startY.HasValue ? startY.Value : cropY;
			width = endX.HasValue ? endX.Value - cropX : width;
			height = endY.HasValue ? endY.Value - cropY : height;

			//cropX = cropX - 20 > 0 ? cropX - 20 : cropX - 10 > 0 ? cropX - 10 : 0;
			//cropY = cropY - 20 > 0 ? cropY - 20 : cropY - 10 > 0 ? cropY - 10 : 0;
			//width = width + 10 < widthFull ? width + 10 : width;
			//height = height + 10 < heightFull ? height + 10 : height;
		}

		static int CountDiffColoredPixelsRow(byte[] data, int stride, int bytesPerPixel, int row, int beginX, int width, MyColor color)
		{
			//var colorGrey = ColorTranslator.FromHtml("#F5F8FA");
			//var colorUseAsWhite = MyColor.FromARGB(colorGrey.A, colorGrey.R, colorGrey.G, colorGrey.B);
			//var colorGrey1 = ColorTranslator.FromHtml("#E6ECF0");
			//var colorUseAsWhite1 = MyColor.FromARGB(colorGrey1.A, colorGrey1.R, colorGrey1.G, colorGrey1.B);
			int counter = 0;
			for (int x = beginX; x < width; ++x)
				try
				{
					if (!GetColor(x, row, stride, data, bytesPerPixel).Equals(color))
						counter++;
					//else if (GetColor(x, row, stride, data, bytesPerPixel).Equals(colorUseAsWhite))
					//	counter += 0;
					//else if (GetColor(x, row, stride, data, bytesPerPixel).Equals(colorUseAsWhite1))
					//	counter += 0;
					//else
					//	counter += 1;
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex);
				}
			return counter;
		}

		static int CountDiffColoredPixelsColumn(byte[] data, int stride, int bytesPerPixel, int col, int beginY, int height, MyColor color)
		{
			//var colorGrey = ColorTranslator.FromHtml("#F5F8FA");
			//var colorUseAsWhite = MyColor.FromARGB(colorGrey.A, colorGrey.R, colorGrey.G, colorGrey.B);
			//var colorGrey1 = ColorTranslator.FromHtml("#E6ECF0");
			//var colorUseAsWhite1 = MyColor.FromARGB(colorGrey1.A, colorGrey1.R, colorGrey1.G, colorGrey1.B);
			int counter = 0;
			for (int y = beginY; y < height; ++y)
				try
				{
					if (GetColor(col, y, stride, data, bytesPerPixel).Equals(color))
						counter++;
					//if (GetColor(col, y, stride, data, bytesPerPixel).Equals(color))
					//	counter += 0;
					//else if (GetColor(col, y, stride, data, bytesPerPixel).Equals(colorUseAsWhite))
					//	counter += 0;
					//else if (GetColor(col, y, stride, data, bytesPerPixel).Equals(colorUseAsWhite1))
					//	counter += 0;
					//else
					//	counter += 1;
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex);
				}
			return counter;
		}

		static int CountDiffColoredPixelsRectangle(Bitmap main, Rectangle rectangle, Color color)
		{
			int stride;
			int bytesPerPixel;
			byte[] data;
			int width = main.Width;
			int height = main.Height;
			BitmapData bmMainData = main.LockBits(new Rectangle(0, 0, main.Width, main.Height), ImageLockMode.ReadOnly, main.PixelFormat);
			stride = bmMainData.Stride;
			bytesPerPixel = Math.Abs(stride) / main.Width;
			data = new byte[Math.Abs(stride) * main.Height];
			System.Runtime.InteropServices.Marshal.Copy(bmMainData.Scan0, data, 0, data.Length);
			main.UnlockBits(bmMainData);

			var colorCheck = MyColor.FromARGB(color.A, color.R, color.G, color.B);
			var colorGrey = ColorTranslator.FromHtml("#F5F8FA");
			var colorUseAsWhite = MyColor.FromARGB(colorGrey.A, colorGrey.R, colorGrey.G, colorGrey.B);
			var colorGrey1 = ColorTranslator.FromHtml("#E6ECF0");
			var colorUseAsWhite1 = MyColor.FromARGB(colorGrey1.A, colorGrey1.R, colorGrey1.G, colorGrey1.B);
			int counter = 0;
			for (int x = rectangle.X; x < Math.Min(width, rectangle.X + rectangle.Width); ++x)
				for (int y = rectangle.Y; y < Math.Min(height, rectangle.Y + rectangle.Height); ++y)
					try
					{
						if (GetColor(x, y, stride, data, bytesPerPixel).Equals(colorCheck))
							counter += 0;
						else if (GetColor(x, y, stride, data, bytesPerPixel).Equals(colorUseAsWhite))
							counter += 0;
						else if (GetColor(x, y, stride, data, bytesPerPixel).Equals(colorUseAsWhite1))
							counter += 0;
						else
							counter += 1;
					}
					catch (Exception ex)
					{
						Console.WriteLine(ex);
					}
			return counter;
		}

		public static SortedDictionary<string, int> CountColorDistribution(Bitmap main)
		{
			int stride;
			int bpp;
			byte[] data;
			int width = main.Width;
			int height = main.Height;
			BitmapData bmMainData = main.LockBits(new Rectangle(0, 0, main.Width, main.Height), ImageLockMode.ReadOnly, main.PixelFormat);
			stride = bmMainData.Stride;
			bpp = Math.Abs(stride) / main.Width;
			data = new byte[Math.Abs(stride) * main.Height];
			System.Runtime.InteropServices.Marshal.Copy(bmMainData.Scan0, data, 0, data.Length);
			main.UnlockBits(bmMainData);
			var retVal = new SortedDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			for (int x = 0; x < width; ++x)
				for (int y = 0; y < height; ++y)
					try
					{
						var htmlColor = ColorTranslator.ToHtml(GetColorRGB(x, y, stride, data, bpp).ToColor());
						if (!retVal.ContainsKey(htmlColor))
							retVal.Add(htmlColor, 1);
						else
							retVal[htmlColor]++;
					}
					catch (Exception ex)
					{
						Console.WriteLine(ex);
					}
			return retVal;
		}


		static MyColor GetColorRGB(int x, int y, int stride, byte[] data, int bitCount)
		{
			int pos = y * stride + x * bitCount;
			byte r = data[pos + 2];
			byte g = data[pos + 1];
			byte b = data[pos + 0];
			return MyColor.FromARGB(0xff, r, g, b);
		}

		static MyColor GetColor(int x, int y, int stride, byte[] data, int bitCount)
		{
			int pos = y * stride + x * bitCount;
			byte a = data[pos + 3];
			byte r = data[pos + 2];
			byte g = data[pos + 1];
			byte b = data[pos + 0];
			return MyColor.FromARGB(a, r, g, b);
		}

		struct MyColor
		{
			byte A;
			byte R;
			byte G;
			byte B;

			public Color ToColor()
			{
				return Color.FromArgb(this.A, this.R, this.G, this.B);
			}

			public static MyColor FromARGB(byte a, byte r, byte g, byte b)
			{
				MyColor mc = new MyColor();
				mc.A = a;
				mc.R = r;
				mc.G = g;
				mc.B = b;
				return mc;
			}

			public override bool Equals(object obj)
			{
				if (!(obj is MyColor))
					return false;
				MyColor color = (MyColor)obj;
				if (color.A == this.A && color.R == this.R && color.G == this.G && color.B == this.B)
					return true;
				if (this.A == color.R && this.R == color.G && this.G == color.B && this.B == color.A)
					return true;
				return false;
			}

			public override int GetHashCode()
			{
				return base.GetHashCode();
			}

			public override string ToString()
			{
				return "0x" + A.ToString("X2") + R.ToString("X2") + G.ToString("X2") + B.ToString("X2");
			}
		}

		static MD5 hasher = MD5.Create();
		static readonly object hasherLock = new object[] { };
		private static string ComputeHash(string text)
		{
			return ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty));
		}

		private static string ComputeHash(byte[] textData)
		{
			byte[] hashData;
			lock (hasherLock)
			{
				try { hashData = hasher.ComputeHash(textData); }
				catch (CryptographicException)
				{
					if (hasher != null) hasher.Dispose();
					hasher = MD5.Create();
					hashData = hasher.ComputeHash(textData);
				}
			}
			return BitConverter.ToString(hashData).Replace("-", string.Empty);
		}
	}

}