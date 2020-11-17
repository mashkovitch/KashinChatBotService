namespace KashinChatBotService
{
	using NLog;
	using OpenQA.Selenium;
	using OpenQA.Selenium.Chrome;
	using OpenQA.Selenium.Remote;
	using System;
	using System.Collections.Generic;
	using System.Collections.Specialized;
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
	using System.Threading.Tasks;
	using System.Web;
	using System.Xml;
	using System.Xml.Linq;
	using Telegram.Bot;
	using Telegram.Bot.Args;
	using Telegram.Bot.Types;
	using Telegram.Bot.Types.Enums;
	using Telegram.Bot.Types.InputFiles;
	using Topshelf;
	using VideoLibrary;
	using WikipediaNet;
	using WikipediaNet.Enums;
	using File = System.IO.File;
	using JsonConvert = Newtonsoft.Json.JsonConvert;

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
		static readonly string[] videos = "DQACAgIAAxkDAAIEfl-hWGkIGlt9ox_aV5HCHoFzu6uLAAMJAAIKYQlJB2yyvqNHbAkeBA,DQACAgIAAxkDAAIEf1-hWG0NZKLp6QTGD-u-N0pB3C7dAAIBCQACCmEJSZZ3aBCNxEZiHgQ,DQACAgIAAxkDAAIEgF-hWG4yLT13doQC3ITCPosStLOOAAICCQACCmEJSTobDAOuB2-jHgQ,DQACAgIAAxkDAAIEgV-hWG4kS9Qd1On9gSa-oZ-ab3yXAAIDCQACCmEJSe51bd9iyaVEHgQ,DQACAgIAAxkDAAIEgl-hWG9D3WHMnhMTboOR6Ez_jd4XAAIECQACCmEJSX4HDOmNplicHgQ,DQACAgIAAxkDAAIEg1-hWHC4g6mPH7m1V6XwmEVCp1pAAAIFCQACCmEJSR_xnUupGljKHgQ".Split(',');
		public bool Start(HostControl hostControl)
		{
			Log = GetLogger();

			Task.Run(() =>
			{
				while (true)
				{
					try
					{
						var channelId = "UC7GcUuO8Z8OBWvJLtQ4d3Sw";
						var data = new WebClient() { Encoding = Encoding.UTF8 }.DownloadData(new Uri("https://www.youtube.com/feeds/videos.xml?channel_id=" + channelId));
						File.WriteAllBytes(Path.Combine(folderCurrent, "yt." + channelId + ".xml"), data);
						using (var stream = new MemoryStream(data))
						{
							int position = 0;
							while ((char)stream.ReadByte() != '<' && position < 50)
								position++;
							if (position == 0)
								stream.Seek(0, SeekOrigin.Begin);
							foreach (var feed in StreamElements(stream, new[] { "entry", "item" }))
							{
								var sl = new NameValueCollection(StringComparer.OrdinalIgnoreCase);
								foreach (var element in feed.Elements())
									try
									{
										if (!String.IsNullOrWhiteSpace(element.Value))
											sl.Add(element.Name.LocalName, element.Value.Trim());
										else if (element.HasAttributes)
											foreach (var attr in element.Attributes())
												try
												{
													if (!String.IsNullOrWhiteSpace(attr.Value))
														sl.Add(element.Name.LocalName + "-" + attr.Name, attr.Value.Trim());
												}
												catch
												{

												}
									}
									catch (Exception ex)
									{
										Log.Error(ex);
									}

								Uri link;
								if (!Uri.TryCreate(sl["link"], UriKind.Absolute, out link)
									&& !Uri.TryCreate(sl["link-href"], UriKind.Absolute, out link)
									&& !Uri.TryCreate(sl["id"], UriKind.Absolute, out link)
									&& !Uri.TryCreate(sl["guid"], UriKind.Absolute, out link))
									continue;
								string pubDate = sl["published"] ?? sl["pubDate"] ?? sl["updated"];
								try { pubDate = DateTime.Parse(pubDate).ToString("yyyy-MM-dd HH:mm"); }
								catch { pubDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm"); }
								if (DateTime.Parse(pubDate).Date != DateTime.UtcNow.Date)
									continue;
								var fileId = Path.Combine(folderCurrent, sl["videoId"]);
								if (File.Exists(fileId))
									continue;
								var author = sl["author"].Split('\n')[0];
								var videos = YouTube.Default.GetAllVideos(link.AbsoluteUri);
								var audio = videos.Where(v => v.AdaptiveKind == AdaptiveKind.Audio && (v.AudioFormat == AudioFormat.Aac || v.AudioFormat == AudioFormat.Mp3)).OrderByDescending(v => v.AudioBitrate).Take(1).FirstOrDefault();
								var title = sl["title"];
								new Task(() =>
								{
									try
									{
										var fileAudio = Path.Combine(folderCurrent, Path.ChangeExtension(audio.FullName, ".m4a"));
										if (!File.Exists(fileAudio) || new FileInfo(fileAudio).Length == 0)
										{
											try { File.Delete(fileAudio); }
											catch { }
											File.WriteAllBytes(fileAudio, audio.GetBytes());
											var fileCover = Path.Combine(folderCurrent, fileId + ".jpg");
											if (!File.Exists(fileCover) || new FileInfo(fileCover).Length == 0)
												try { new WebClient().DownloadFile("https://i1.ytimg.com/vi/" + fileId + "/hqdefault.jpg", fileCover); }
												catch { }
											if (File.Exists(fileCover) && new FileInfo(fileCover).Length > 0)
											{
												string[] consoleOk, consoleErr;
												Environment.CurrentDirectory = folderCurrent;
												ExecuteProcess("tageditor-3.3.9-x86_64-w64-mingw32.exe", "-s title=\"" + title.Replace(@"""", @"\""") + "\" artist=\"" + author.Replace(@"""", @"\""") + "\" cover=\"" + fileCover + "\" -f \"" + fileAudio + "\"", TimeSpan.FromMinutes(5), out consoleOk, out consoleErr);
											}
										}
										using (var fs = File.OpenRead(fileAudio))
											Bot.SendAudioAsync(new ChatId(-1001331192169), new InputOnlineFile(fs, Path.GetFileName(fileAudio)), caption: "#anotherkashin " + ShortenUrl(link.AbsoluteUri), title: audio.Title, performer: author, duration: (int)((double)audio.ContentLength / (double)audio.AudioBitrate)).Wait();
										File.WriteAllText(Path.Combine(folderCurrent, sl["videoId"]), DateTime.Now.Ticks.ToString());
									}
									catch (Exception ex)
									{
										Log.Error(ex);
									}
								}).Start();
							}
						}
					}
					catch (Exception ex)
					{
						Log.Error(ex);
					}
					try
					{
						var channelId = "UCRiMhZrS2VNyHVMQjQeSU4A";
						var data = new WebClient() { Encoding = Encoding.UTF8 }.DownloadData(new Uri("https://www.youtube.com/feeds/videos.xml?channel_id=" + channelId));
						File.WriteAllBytes(Path.Combine(folderCurrent, "yt." + channelId + ".xml"), data);
						using (var stream = new MemoryStream(data))
						{
							int position = 0;
							while ((char)stream.ReadByte() != '<' && position < 50)
								position++;
							if (position == 0)
								stream.Seek(0, SeekOrigin.Begin);
							foreach (var feed in StreamElements(stream, new[] { "entry", "item" }))
							{
								var sl = new NameValueCollection(StringComparer.OrdinalIgnoreCase);
								foreach (var element in feed.Elements())
									try
									{
										if (!String.IsNullOrWhiteSpace(element.Value))
											sl.Add(element.Name.LocalName, element.Value.Trim());
										else if (element.HasAttributes)
											foreach (var attr in element.Attributes())
												try
												{
													if (!String.IsNullOrWhiteSpace(attr.Value))
														sl.Add(element.Name.LocalName + "-" + attr.Name, attr.Value.Trim());
												}
												catch
												{

												}
									}
									catch (Exception ex)
									{
										Log.Error(ex);
									}

								Uri link;
								if (!Uri.TryCreate(sl["link"], UriKind.Absolute, out link)
									&& !Uri.TryCreate(sl["link-href"], UriKind.Absolute, out link)
									&& !Uri.TryCreate(sl["id"], UriKind.Absolute, out link)
									&& !Uri.TryCreate(sl["guid"], UriKind.Absolute, out link))
									continue;
								var title = sl["title"];
								if (!title.ToUpper().Contains("КАШИН"))
									continue;
								string pubDate = sl["published"] ?? sl["pubDate"] ?? sl["updated"];
								try { pubDate = DateTime.Parse(pubDate).ToString("yyyy-MM-dd HH:mm"); }
								catch { pubDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm"); }
								if (DateTime.Parse(pubDate).Date != DateTime.UtcNow.Date)
									continue;
								var fileId = Path.Combine(folderCurrent, sl["videoId"]);
								if (File.Exists(fileId))
									continue;
								var author = sl["author"].Split('\n')[0];
								var videos = YouTube.Default.GetAllVideos(link.AbsoluteUri);
								var audio = videos.Where(v => v.AdaptiveKind == AdaptiveKind.Audio && (v.AudioFormat == AudioFormat.Aac || v.AudioFormat == AudioFormat.Mp3)).OrderByDescending(v => v.AudioBitrate).Take(1).FirstOrDefault();
								new Task(() =>
								{
									try
									{
										var fileAudio = Path.Combine(folderCurrent, Path.ChangeExtension(audio.FullName, ".m4a"));
										if (!File.Exists(fileAudio) || new FileInfo(fileAudio).Length == 0)
										{
											try { File.Delete(fileAudio); }
											catch { }
											File.WriteAllBytes(fileAudio, audio.GetBytes());
											var fileCover = Path.Combine(folderCurrent, fileId + ".jpg");
											if (!File.Exists(fileCover) || new FileInfo(fileCover).Length == 0)
												try { new WebClient().DownloadFile("https://i1.ytimg.com/vi/" + fileId + "/hqdefault.jpg", fileCover); }
												catch { }
											if (File.Exists(fileCover) && new FileInfo(fileCover).Length > 0)
											{
												string[] consoleOk, consoleErr;
												Environment.CurrentDirectory = folderCurrent;
												ExecuteProcess("tageditor-3.3.9-x86_64-w64-mingw32.exe", "-s title=\"" + title.Replace(@"""", @"\""") + "\" artist=\"" + author.Replace(@"""", @"\""") + "\" cover=\"" + fileCover + "\" -f \"" + fileAudio + "\"", TimeSpan.FromMinutes(5), out consoleOk, out consoleErr);
											}
										}
										using (var fs = File.OpenRead(fileAudio))
											Bot.SendAudioAsync(new ChatId(-1001331192169), new InputOnlineFile(fs, Path.GetFileName(fileAudio)), caption: "#Кашин " + ShortenUrl(link.AbsoluteUri), title: audio.Title, performer: author, duration: (int)((double)audio.ContentLength / (double)audio.AudioBitrate)).Wait();
										File.WriteAllText(Path.Combine(folderCurrent, sl["videoId"]), DateTime.Now.Ticks.ToString());
									}
									catch (Exception ex)
									{
										Log.Error(ex);
									}
								}).Start();
							}
						}
					}
					catch (Exception ex)
					{
						Log.Error(ex);
					}
					Thread.Sleep(TimeSpan.FromMinutes(5));
				}
			});
			//StickerPack();
			//GetStat();
			ProcessMessages();
			return true;
		}

		static string GetTitle(string titleText)
		{
			if (String.IsNullOrWhiteSpace(titleText))
				return "No title";
			var title = Regex.Replace(titleText, @"\s+", " ").Trim();
			return title;//.Substring(0, Math.Min(25, title.Length)).PadLeft(25, ' ');
		}

		static IEnumerable<XElement> StreamElements(Stream xml, string[] names)
		{
			var settings = new XmlReaderSettings { NameTable = new NameTable() };
			settings.ConformanceLevel = ConformanceLevel.Fragment;
			settings.DtdProcessing = DtdProcessing.Ignore;
			var xmlns = new XmlNamespaceManager(settings.NameTable);
			var context = new XmlParserContext(null, xmlns, string.Empty, XmlSpace.Default, Encoding.UTF8);
			var elementNames = new SortedSet<string>(names, StringComparer.OrdinalIgnoreCase);
			using (XmlReader reader = XmlReader.Create(xml, settings, context))
			{
				//reader.MoveToContent();
				while (reader.Read())
					if (reader.NodeType == XmlNodeType.Element && elementNames.Contains(reader.Name))
					{
						XElement element = null;
						try
						{
							element = XElement.ReadFrom(reader) as XElement;
						}
						catch
						{
							yield break;
						}
						yield return element;
					}
			}
			yield break;
		}

		private void ProcessMessages()
		{
			var meName = "@" + Bot.GetMeAsync().Result.Username;
			Console.WriteLine(meName);
			Bot.SetWebhookAsync("").Wait();
			KillProcess(BinaryLocation, null);
			KillProcess(DriverLocation, null);
			ChromeDriver driver = new ChromeDriver(Path.GetDirectoryName(DriverLocation), ChromeOptionsBase, TimeSpan.FromMinutes(10));
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
						Bot.SendStickerAsync(message.Chat.Id, new InputOnlineFile("CAACAgIAAxUAAV-o5mqpsnMAAbXOOZ7IJUi8BQMYNAACoAsAAiO43whg0hU1D7uR1B4E"), replyToMessageId: message.MessageId).Wait();
						return;
					}
					if (message.Type == MessageType.Document
						&& !string.IsNullOrWhiteSpace(message.Caption)
						&& Path.GetExtension(message.Document.FileName).ToLower().EndsWith("tf", StringComparison.OrdinalIgnoreCase))
					{
						Bot.SendChatActionAsync(message.Chat.Id, ChatAction.UploadDocument).Wait();
						var fileFont = Path.Combine(folderCurrent, message.Document.FileName);
						if (!File.Exists(fileFont) || new FileInfo(fileFont).Length == 0)
							using (var fs = File.Create(Path.Combine(folderCurrent, message.Document.FileName)))
								Bot.DownloadFileAsync(Bot.GetFileAsync(message.Document.FileId).Result.FilePath, fs).Wait();
						var messageText = message.Caption;
						string fileId;
						var fileResult = CreateSticker(string.IsNullOrWhiteSpace(messageText) ? "РУССКИЕ\nВПЕРЕД!" : messageText, fileFont, Color.White, ColorTranslator.FromHtml("#e0312c"), null, out fileId);
						if (string.IsNullOrWhiteSpace(fileId))
							using (var fs = File.OpenRead(fileResult))
								Bot.SendStickerAsync(message.Chat.Id, new InputOnlineFile(fs)).Wait();
						else
							Bot.SendStickerAsync(message.Chat.Id, new InputOnlineFile(fileId)).Wait();
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
							if (command.EndsWith(meName, StringComparison.OrdinalIgnoreCase))
								command = command.Substring(0, command.Length - meName.Length);
							if (command == "/yt")
							{
								//http://www.youtube.com/get_video_info?&video_id=OhSqR2FcsxY
								//var fileCache = Path.Combine(folderCurrent, ScreenShooter.ComputeHash(messageText) + ".htm");
								try
								{
									var yt = Encoding.UTF8.GetString(new WebClient().DownloadData(messageText));
									//	var links = new SortedSet<string>();
									var videoTitle = HttpUtility.HtmlDecode(Regex.Match(yt, "<title>([^<]+)", RegexOptions.IgnoreCase).Groups[1].Value).Trim();
									var author = string.Empty;
									try { author = HttpUtility.HtmlDecode(Regex.Match(yt, @"ownerChannelName\"":\""([^""]+)", RegexOptions.IgnoreCase).Groups[1].Value).Trim(); }
									catch (Exception ex)
									{
										Log.Error(ex);
									}
									var id = Regex.Match(yt, @"<meta\s+itemprop=""videoId""\s+content=""([^""]+)", RegexOptions.IgnoreCase).Groups[1].Value;
									var fileCover = Path.Combine(folderCurrent, id + ".jpg");
									try
									{
										var cover = Regex.Match(yt, @"href=""([^""]+maxresdefault.jpg)""", RegexOptions.IgnoreCase).Groups[1].Value;
										if (string.IsNullOrWhiteSpace(cover))
											cover = Regex.Match(yt, @"href=""([^""]+hqdefault.jpg)""", RegexOptions.IgnoreCase).Groups[1].Value;
										new WebClient().DownloadFile(cover, fileCover);
									}
									catch (Exception ex)
									{
										Log.Error(ex);
									}
									//	var audioUrl = string.Empty;
									//	TimeSpan? duration = null;
									//	foreach (var format in new[] { "adaptiveFormats", "formats" })
									//		foreach (Match mItag in Regex.Matches(yt, string.Format(@"\\""{0}\\"":(\[[^\]]+\])",format), RegexOptions.IgnoreCase))
									//			try
									//			{
									//				dynamic formats = JsonConvert.DeserializeObject(mItag.Groups[1].Value.Replace(@"\\\""","'").Replace("\\",string.Empty));
									//				foreach (var json in formats)
									//					try
									//					{
									//						ulong resolution = 0;
									//						try { resolution = uint.Parse(json.width.ToString()) * uint.Parse(json.height.ToString()); }
									//						catch (Exception ex)
									//						{
									//							Log.Error(ex);
									//						}
									//						Int64 contentLength = 0;
									//						try { contentLength = Int64.Parse(json.contentLength.ToString()); }
									//						catch { }
									//						if (contentLength == 0)
									//							continue;
									//						var mimeType = json.mimeType.ToString().Split(';')[0];
									//						if (!mimeType.EndsWith("/mp4"))
									//							continue;
									//						var indexOf = 0;
									//						try { indexOf = json.signatureCipher.ToString().IndexOf("https:"); }
									//						catch { }
									//						var url = HttpUtility.UrlDecode((json.url == null ? (json.signatureCipher.ToString().Substring(indexOf) + "&" + json.signatureCipher.ToString().Substring(0,indexOf)) : json.url).ToString().Replace("u0026", "&"));
									//						if (mimeType == "audio/mp4")
									//						{
									//							audioUrl = url;
									//							links.Add(resolution + "@" + mimeType
									//								+ (resolution > 0 ? (", " + json.width + "x" + json.height) : string.Empty)
									//								+ " / " + SizeSuffix(Int64.Parse(json.averageBitrate.ToString())) + "/сек."
									//								+ ", " + ShortenUrl(url)
									//								+ " (" + SizeSuffix(contentLength) + ")");
									//							if (!duration.HasValue && !string.IsNullOrWhiteSpace((json.approxDurationMs ?? "").ToString()))
									//								duration = TimeSpan.FromMilliseconds(double.Parse(json.approxDurationMs.ToString()));
									//						}
									//					}
									//					catch (Exception ex)
									//					{
									//						Log.Error(ex);
									//					}
									//			}
									//			catch (Exception ex)
									//			{
									//				Log.Error(ex);
									//			}
									var videos = YouTube.Default.GetAllVideos(messageText);
									var audio = videos.Where(v => v.AdaptiveKind == AdaptiveKind.Audio && (v.AudioFormat == AudioFormat.Aac || v.AudioFormat == AudioFormat.Mp3)).OrderByDescending(v => v.AudioBitrate).Take(1).FirstOrDefault();
									var links = new SortedSet<string>();
									foreach (var v in videos.Where(v => v.Resolution > 0 && v.AudioBitrate > 0 && v.ContentLength.HasValue).OrderByDescending(v => v.ContentLength))
										links.Add("видео, " + ShortenUrl(v.Uri) + ", (" + SizeSuffix(v.ContentLength.Value) + "), " + v.Resolution + " / " + v.AudioBitrate);
									Bot.SendTextMessageAsync(message.Chat.Id, "аудио, " + ShortenUrl(audio.Uri) + ", (" + SizeSuffix(audio.ContentLength ?? 0) + ")" + (links.Count == 0 ? "" : (Environment.NewLine + string.Join(Environment.NewLine, links.ToArray()))), replyToMessageId: message.MessageId, disableWebPagePreview: true).Wait();
									new Task(() =>
									{
										try
										{
											var fileAudio = Path.Combine(folderCurrent, Path.ChangeExtension(audio.FullName, ".m4a"));
											if (!File.Exists(fileAudio) || new FileInfo(fileAudio).Length == 0)
											{
												try { File.Delete(fileAudio); }
												catch { }
												File.WriteAllBytes(fileAudio, audio.GetBytes());
												if (File.Exists(fileCover) && new FileInfo(fileCover).Length > 0)
												{
													string[] consoleOk, consoleErr;
													Environment.CurrentDirectory = folderCurrent;
													ExecuteProcess("tageditor-3.3.9-x86_64-w64-mingw32.exe", "-s title=\"" + videoTitle.Replace(@"""", @"\""") + "\" artist=\"" + author.Replace(@"""", @"\""") + "\" cover=\"" + fileCover + "\" -f \"" + fileAudio + "\"", TimeSpan.FromMinutes(5), out consoleOk, out consoleErr);
													//	AddMp3Tags(fileAudio, fileCover, videoTitle, new[] { author });
												}
											}
											using (var fs = File.OpenRead(fileAudio))
												Bot.SendAudioAsync(message.Chat.Id, new InputOnlineFile(fs, Path.GetFileName(fileAudio)), messageText, title: audio.Title, performer: author, duration: (int)((double)audio.ContentLength / (double)audio.AudioBitrate)).Wait();
										}
										catch (Exception ex)
										{
											Bot.SendTextMessageAsync(message.Chat.Id, "Не удалось выгрузить аудио-версию: " + ShortenUrl(audio.Uri) + ", ошибка: " + ex.ToString(), replyToMessageId: message.MessageId, disableWebPagePreview: true).Wait();
										}
									}).Start();
								}
								catch (Exception ex)
								{
									Bot.SendTextMessageAsync(message.Chat.Id, "Не удалось выгрузить информацию о ролике: " + ex.ToString(), replyToMessageId: message.MessageId, disableWebPagePreview: true).Wait();
								}
								return;
							}
							else
							if ((command == "/sticker" || command == "/s") && message.Text.StartsWith("/s"))
							{
								//using (var ms = new MemoryStream())
								//{
								//	using (var img = Image.FromFile(CreateSticker(string.IsNullOrWhiteSpace(messageText) ? "РУССКИЕ\nВПЕРЕД!" : messageText)))
								//	using (var imageFactory = new ImageFactory(preserveExifData: false))
								//		imageFactory.Load(img).Format(new WebPFormat()).Quality(100).Save(ms);
								//	Bot.SendStickerAsync(message.Chat.Id, new InputOnlineFile(ms)).Wait();
								//}
								string fileId;
								var fileResult = CreateSticker(string.IsNullOrWhiteSpace(messageText) ? "РУССКИЕ\nВПЕРЕД!" : messageText, Path.Combine(folderCurrent, "FreeSet-ExtraBoldOblique.otf"), Color.White, ColorTranslator.FromHtml("#e0312c"), null, out fileId);
								if (string.IsNullOrWhiteSpace(fileId))
									using (var fs = File.OpenRead(fileResult))
										Bot.SendStickerAsync(message.Chat.Id, new InputOnlineFile(fs)).Wait();
								else
									Bot.SendStickerAsync(message.Chat.Id, new InputOnlineFile(fileId)).Wait();
								return;
							}
							else if ((command == "/st") && message.Text.StartsWith("/st"))
							{
								string fileId;
								var fileResult = CreateSticker(string.IsNullOrWhiteSpace(messageText) ? "РУССКИЕ\nВПЕРЕД!" : messageText, Path.Combine(folderCurrent, "FreeSet-ExtraBoldOblique.otf"), Color.White, ColorTranslator.FromHtml("#e0312c"), messageText.Split(' ')[0], out fileId);
								if (string.IsNullOrWhiteSpace(fileId))
									using (var fs = File.OpenRead(fileResult))
										Bot.SendStickerAsync(message.Chat.Id, new InputOnlineFile(fs)).Wait();
								else
									Bot.SendStickerAsync(message.Chat.Id, new InputOnlineFile(fileId)).Wait();
								return;
							}
							else if ((command == "/sl") && message.Text.StartsWith("/sl"))
							{
								string fileId;
								var fileResult = CreateSticker(string.IsNullOrWhiteSpace(messageText) ? "РУССКИЕ\nВПЕРЕД!" : messageText, Path.Combine(folderCurrent, "Lineatura.ttf"), Color.White, ColorTranslator.FromHtml("#e0312c"), null, out fileId);
								if (string.IsNullOrWhiteSpace(fileId))
									using (var fs = File.OpenRead(fileResult))
										Bot.SendStickerAsync(message.Chat.Id, new InputOnlineFile(fs)).Wait();
								else
									Bot.SendStickerAsync(message.Chat.Id, new InputOnlineFile(fileId)).Wait();
								return;
							}
							else if ((command == "/sb") && message.Text.StartsWith("/sb"))
							{
								string fileId;
								var fileResult = CreateSticker(string.IsNullOrWhiteSpace(messageText) ? "РУССКИЕ\nВПЕРЕД!" : messageText, Path.Combine(folderCurrent, "Lineatura.ttf"), Color.White, Color.Black, null, out fileId);
								if (string.IsNullOrWhiteSpace(fileId))
									using (var fs = File.OpenRead(fileResult))
										Bot.SendStickerAsync(message.Chat.Id, new InputOnlineFile(fs)).Wait();
								else
									Bot.SendStickerAsync(message.Chat.Id, new InputOnlineFile(fileId)).Wait();
								return;
							}
							else if ((command == "/sw") && message.Text.StartsWith("/sw"))
							{
								string fileId;
								var fileResult = CreateSticker(string.IsNullOrWhiteSpace(messageText) ? "РУССКИЕ\nВПЕРЕД!" : messageText, Path.Combine(folderCurrent, "Lineatura.ttf"), Color.Black, Color.White, null, out fileId);
								if (string.IsNullOrWhiteSpace(fileId))
									using (var fs = File.OpenRead(fileResult))
										Bot.SendStickerAsync(message.Chat.Id, new InputOnlineFile(fs)).Wait();
								else
									Bot.SendStickerAsync(message.Chat.Id, new InputOnlineFile(fileId)).Wait();
								return;
							}
							else if ((command == "/sp") && message.Text.StartsWith("/sp"))
							{
								string fileId;
								var fileResult = CreateSticker(string.IsNullOrWhiteSpace(messageText) ? "РУССКИЕ\nВПЕРЕД!" : messageText, Path.Combine(folderCurrent, "PRAVDA.otf"), Color.White, ColorTranslator.FromHtml("#e0312c"), null, out fileId);
								if (string.IsNullOrWhiteSpace(fileId))
									using (var fs = File.OpenRead(fileResult))
										Bot.SendStickerAsync(message.Chat.Id, new InputOnlineFile(fs)).Wait();
								else
									Bot.SendStickerAsync(message.Chat.Id, new InputOnlineFile(fileId)).Wait();
								return;
							}
							else if ((command == "/spb") && message.Text.StartsWith("/spb"))
							{
								string fileId;
								var fileResult = CreateSticker(string.IsNullOrWhiteSpace(messageText) ? "РУССКИЕ\nВПЕРЕД!" : messageText, Path.Combine(folderCurrent, "PRAVDA.otf"), Color.White, Color.Black, null, out fileId);
								if (string.IsNullOrWhiteSpace(fileId))
									using (var fs = File.OpenRead(fileResult))
										Bot.SendStickerAsync(message.Chat.Id, new InputOnlineFile(fs)).Wait();
								else
									Bot.SendStickerAsync(message.Chat.Id, new InputOnlineFile(fileId)).Wait();
								return;
							}
							else if ((command == "/spw") && message.Text.StartsWith("/spw"))
							{
								string fileId;
								var fileResult = CreateSticker(string.IsNullOrWhiteSpace(messageText) ? "РУССКИЕ\nВПЕРЕД!" : messageText, Path.Combine(folderCurrent, "PRAVDA.otf"), Color.Black, Color.White, null, out fileId);
								if (string.IsNullOrWhiteSpace(fileId))
									using (var fs = File.OpenRead(fileResult))
										Bot.SendStickerAsync(message.Chat.Id, new InputOnlineFile(fs)).Wait();
								else
									Bot.SendStickerAsync(message.Chat.Id, new InputOnlineFile(fileId)).Wait();
								return;
							}
							else if (command == "/wi" && message.Text.StartsWith("/wi"))
							{
								var wikipedia = new Wikipedia() { Language = Language.Russian, UseTLS = true, Limit = 1, What = What.Text };
								try
								{
									var results = wikipedia.Search(messageText);
									foreach (var s in results.Search)
									{
										var snippet = s.Snippet ?? "";
										try { snippet = Regex.Replace(Regex.Replace(s.Snippet, "<[^>]+>", " "), @"\s+", " "); }
										catch { }
										Bot.SendTextMessageAsync(message.Chat.Id, s.Title + "\r\n" + snippet + "\r\n" + s.Url, replyToMessageId: message.MessageId, disableWebPagePreview: true).Wait();
									}
								}
								catch { Bot.SendTextMessageAsync(message.Chat.Id, "Ничего не смог найти для вас!", replyToMessageId: message.MessageId, disableWebPagePreview: true).Wait(); }
								return;
							}
							else if (command == "/ss" && message.Text.StartsWith("/ss"))
							{
								if (evu.Update.Message.Entities != null)
								{
									foreach (MessageEntity linkEntity in evu.Update.Message.Entities.Where(entity => entity.Type == MessageEntityType.Url))
										try
										{
											var link = new Uri(message.Text.Substring(linkEntity.Offset, linkEntity.Length));
											var host = link.Host.ToLower();
											if (host.Contains("twitter.com") || host.Contains("facebook.com") || host.Contains("fb.com") || host.Contains("instagram.com") || host.Contains("livejournal.com") || host.Contains("vk.com"))
												try
												{
													var shortLink = ShortenUrl(link.AbsoluteUri);//host.Contains("facebook.com") || host.Contains("fb.com") ? GetFBID(link.AbsoluteUri) : 
													var fileImage = Path.Combine(FolderCurrent, ScreenShooter.ComputeHash(shortLink) + ".png");
													if (!File.Exists(fileImage) || new FileInfo(fileImage).Length == 0)
														try
														{
															fileImage = ScreenShooter.Get(link.AbsoluteUri, shortLink, FolderCurrent, driver);
														}
														catch (Exception ex)
														{
															Log.Error(new Exception(link.AbsoluteUri, ex));
															KillProcess(BinaryLocation, null);
															KillProcess(DriverLocation, null);
															driver = new ChromeDriver(Path.GetDirectoryName(DriverLocation), ChromeOptionsBase, TimeSpan.FromMinutes(10));
														}
													using (var fs = File.OpenRead(fileImage))
														Bot.SendPhotoAsync(message.Chat.Id
															, new InputOnlineFile(fs)
															, link.AbsoluteUri//host.Contains("facebook.com") || host.Contains("fb.com") ? GetFBID(link.AbsoluteUri) : 
															, replyToMessageId: message.MessageId
															, disableNotification: true).Wait();
												}
												catch (Exception ex) { Log.Error(ex); }
										}
										catch (Exception ex) { Log.Error(ex); }
									return;
								}
							}
							else if (command == "/h")
								Bot.SendTextMessageAsync(
									chatId: message.Chat.Id,
									text: "Usage:\n" +
											"/s	текст   - стикер \"FreeSet-Extra\"\n" +
											"/sl текст  - стикер \"Lineatura\"\n" +
											"/sb текст  - стикер \"Lineatura\" (черный фон)\n" +
											"/sw текст  - стикер \"Lineatura\" (белый фон)\n" +
											"/sp текст  - стикер \"Правда\"\n" +
											"/spb текст - стикер \"Правда\" (черый фон)\n" +
											"/spw текст - стикер \"Правда\" (белый фон)\n" +
											"/ss адрес	- скрин сообщения из соцсетей (ТВ,ВК,ИН,ФБ,ЖЖ)\n" +
											"/wi текст  - запрос в Википедию\n" +
											"/yt адрес  - получить ссылки на звук и видео youtube-ролика"
								//+ "/yta адрес - выгрузить звуковую дорожку youtube-ролика"
								).Wait();
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
						foreach (MessageEntity linkEntity in evu.Update.Message.Entities.Where(entity => entity.Type == MessageEntityType.Url).Take(1))
							try
							{
								var link = new Uri(message.Text.Substring(linkEntity.Offset, linkEntity.Length));
								var host = link.Host.ToLower();
								if (host.Contains("facebook.com") || host.Contains("fb.com"))//host.Contains("twitter.com") || host.Contains("livejournal.com") || host.Contains("vk.com"))
									try
									{
										var shortLink = host.Contains("facebook.com") || host.Contains("fb.com") ? GetFBID(link.AbsoluteUri) : ShortenUrl(link.AbsoluteUri);
										var fileImage = Path.Combine(FolderCurrent, ScreenShooter.ComputeHash(shortLink) + ".png");
										if (!File.Exists(fileImage) || new FileInfo(fileImage).Length == 0)
											try
											{
												fileImage = ScreenShooter.Get(link.AbsoluteUri, shortLink, FolderCurrent, driver);
											}
											catch (Exception ex)
											{
												Log.Error(new Exception(link.AbsoluteUri, ex));
												KillProcess(BinaryLocation, null);
												KillProcess(DriverLocation, null);
												driver = new ChromeDriver(Path.GetDirectoryName(DriverLocation), ChromeOptionsBase, TimeSpan.FromMinutes(10));
											}
										using (var fs = File.OpenRead(fileImage))
											Bot.SendPhotoAsync(message.Chat.Id, new InputOnlineFile(fs), link.AbsoluteUri, /*replyToMessageId: message.MessageId, */disableNotification: true).Wait();//host.Contains("facebook.com") || host.Contains("fb.com") ? GetFBID(link.AbsoluteUri) : 
									}
									catch (Exception ex) { Log.Error(ex); }
								return;
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
			KillProcess(BinaryLocation, null);
			KillProcess(DriverLocation, null);
			return true;
		}

		public void AddMp3Tags(string fileAudio, string fileCover, string title, string[] performers)
		{
			TagLib.Id3v2.Tag.DefaultVersion = 3;
			TagLib.Id3v2.Tag.ForceDefaultVersion = true;
			using (var file = TagLib.File.Create(fileAudio))
			{
				file.Tag.Pictures = new[] { new TagLib.Id3v2.AttachmentFrame
					{
						Type = TagLib.PictureType.FrontCover,
						Description = "Cover",
						MimeType = System.Net.Mime.MediaTypeNames.Image.Jpeg,
						Filename = Path.GetFileName(fileCover),
						Data = File.ReadAllBytes(fileCover),
						TextEncoding = TagLib.StringType.UTF16
					}
				};
				file.Tag.Title = title;
				file.Tag.Performers = performers;
				file.Tag.Year = (uint)DateTime.UtcNow.Year;
				file.Tag.DateTagged = DateTime.UtcNow;
				file.RemoveTags(file.TagTypes & ~file.TagTypesOnDisk);
				file.Save();
			}
		}
		static void GetStat()
		{
			var userHours = new SortedDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			var dates = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
			var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
			var userCount = new SortedDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			foreach (var file in Directory.EnumerateFiles(@"C:\Users\HP\Downloads\Telegram Desktop\ChatExport_2020-11-15", "*.json", SearchOption.AllDirectories))
			{
				dynamic json = JsonConvert.DeserializeObject(File.ReadAllText(file));
				foreach (var message in json.messages)
					try
					{
						if (message.type.ToString() != "message")
						{
							Console.WriteLine(message.action.ToString());
							continue;
						}
						var userName = message.from.ToString();
						var text = (message.text ?? "").ToString();
						var date = (DateTime)message.date;
						var key = userName + "@" + date.ToString("yyyyMMddHH");
						var count = 0;
						foreach (var word in Regex.Split(text, @"\s+"))
							if (obsceneCorpus.Contains(word))
								count++;
						if (!userHours.ContainsKey(key))
							userHours.Add(key, count);
						else
							userHours[key] += count;
						dates.Add(date.ToString("yyyyMMddHH"));
						names.Add(userName);
						if (!userCount.ContainsKey(userName))
							userCount.Add(userName, count);
						else
							userCount[userName] += count;
					}
					catch (Exception ex) { Console.WriteLine(ex); }
			}
			var first = DateTime.ParseExact(dates.First(), "yyyyMMddHH", CultureInfo.InvariantCulture);
			var last = DateTime.ParseExact(dates.Last(), "yyyyMMddHH", CultureInfo.InvariantCulture);
			var hoursTotal = last.Subtract(first).TotalHours;
			using (var sw = new StreamWriter(@"stat.htm", false, Encoding.UTF8))
			{
				sw.Write("<table border='1'><tr><th>User</th><th>Total</th><th>Avg</th>");
				for (DateTime d = first; d <= last; d = d.AddHours(1))
					sw.Write("<th>" + d.ToString("dd.MM.yy HH:mm") + "</th>");
				sw.WriteLine("</tr>");
				foreach (var user in userCount.OrderByDescending(u => u.Value).Select(s => s.Key))
				{
					sw.Write("<tr><th>" + user.Split('@')[0] + "</th>");
					sw.Write("<td>" + userCount[user] + "</td>");
					sw.Write("<td>" + (int)(userCount[user] / hoursTotal) + "</td>");
					for (DateTime d = first; d <= last; d = d.AddHours(1))
					{
						var key = user + "@" + d.ToString("yyyyMMddHH");
						var val = (userHours.ContainsKey(key) ? userHours[key] : 0);
						sw.Write("<td" + (val > 0 ? " style='background-color:red'>" : ">") + val + "</td>");
					}
					sw.WriteLine("</tr>");
				}
				sw.Write("</table>");
			}
			using (var sw = new StreamWriter(@"stat.csv", false, Encoding.UTF8))
			{
				sw.Write("User;Total;Avg");
				for (DateTime d = first; d <= last; d = d.AddHours(1))
					sw.Write(";" + d.ToString("dd.MM.yy HH:mm"));
				sw.WriteLine();
				foreach (var user in userCount.OrderByDescending(u => u.Value).Select(s => s.Key))
				{
					sw.Write(user.Split('@')[0] + ";");
					sw.Write(userCount[user] + ";");
					sw.Write((int)(userCount[user] / hoursTotal));
					for (DateTime d = first; d <= last; d = d.AddHours(1))
					{
						var key = user + "@" + d.ToString("yyyyMMddHH");
						var val = (userHours.ContainsKey(key) ? userHours[key] : 0);
						sw.Write(";" + val);
					}
					sw.WriteLine();
				}
			}
		}
		static string GetTwitterImage(string twitterName)
		{
			var file = Path.Combine(FolderCurrent, twitterName + ".jpg");
			if (!File.Exists(file) || new FileInfo(file).Length == 0)
			{
				ChromeDriver driver = null;
				try
				{
					driver = new ChromeDriver(Path.GetDirectoryName(DriverLocation), ChromeOptionsBase, TimeSpan.FromMinutes(10));
					driver.Navigate().GoToUrl("https://twitter.com/" + twitterName + "/photo");
					Thread.Sleep(7000);
					var s = driver.PageSource;
					var imgs = driver.FindElements(By.XPath("//img[@src]"));
					var img = imgs[imgs.Count - 1];
					new WebClient().DownloadFile(img.GetAttribute("src"), file);
				}
				catch (Exception ex) { Log.Error(ex); }
				finally
				{
					try { driver.Close(); } catch { }
					try { driver.Quit(); } catch { }
				}
			}
			return file;
		}


		static string CreateSticker(string messageText, string fileFont, Color textColor, Color backColor, string twitterName, out string fileId)
		{
			fileId = string.Empty;
			var slotUrl = messageText;
			if (!string.IsNullOrWhiteSpace(twitterName))
				slotUrl = slotUrl.Substring(twitterName.Length).Trim();
			var foo = new PrivateFontCollection();
			foo.AddFontFile(fileFont);
			var fontSize = 96f;
			var font = new Font((FontFamily)foo.Families[0], fontSize);
			SizeF labelMeasure;
			//SizeF labelMeasureStable;
			using (Bitmap result = new Bitmap(512, 512, PixelFormat.Format24bppRgb))
			{
				result.SetResolution(96f, 96f);
				using (Graphics graphics = Graphics.FromImage(result))
					labelMeasure = graphics.MeasureString(slotUrl, font);
			}
			var rowsCount = Regex.Split(slotUrl, @"\r?\n").Length;
			var b = (int)Math.Ceiling((labelMeasure.Height * 18) / 90);
			var leftOffset = 0;
			var bytes = new byte[0];
			if (!string.IsNullOrWhiteSpace(twitterName))
				try
				{
					using (var img = Image.FromFile(GetTwitterImage(twitterName)))
					using (var imgTwi = new Bitmap(img.Width, img.Height, PixelFormat.Format24bppRgb))
					{
						imgTwi.MakeTransparent(Color.Transparent);
						imgTwi.SetResolution(96f, 96f);
						using (var imgGraphics = Graphics.FromImage(imgTwi))
						{
							imgGraphics.Clear(Color.Transparent);
							imgGraphics.CompositingQuality = CompositingQuality.HighQuality;
							imgGraphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
							imgGraphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
							imgGraphics.SmoothingMode = SmoothingMode.HighQuality;
							DrawRndRect(imgGraphics, new RectangleF(0, 0, (int)imgTwi.Height, (int)imgTwi.Height), new TextureBrush(img), (int)img.Height / 2);
							//imgGraphics.DrawImage(img, new Rectangle(0, 0, imgTwi.Width, imgTwi.Height), new Rectangle(30, 0, img.Width, img.Height), GraphicsUnit.Pixel);
						}
						bytes = (ImageResize.SaveAs(imgTwi, ImageFormat.Png, 100));
						leftOffset = ((int)labelMeasure.Height + (rowsCount * 10)) / 2;
					}
				}
				catch (Exception ex)
				{
					Log.Error(ex);
				}

			using (var result = new Bitmap((int)labelMeasure.Width + leftOffset * 2 + b * 2, (int)labelMeasure.Height + (rowsCount * 10), PixelFormat.Format24bppRgb))
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
					if (!string.IsNullOrWhiteSpace(twitterName) && bytes.Length > 0)
						try
						{
							using (var ms = new MemoryStream(bytes))
							using (var imgTwi = Image.FromStream(ms))
								graphics.DrawImage(imgTwi, new RectangleF(b, 10, (int)result.Height - 20, (int)result.Height - 20));
							leftOffset = result.Height / 2;
						}
						catch (Exception ex)
						{
							Log.Error(ex);
						}
					var offset = 10;
					var barWidth = result.Width - b * 2;
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
						var textLeft = leftOffset + b;
						if (!leftAlign)
							textLeft += (int)(barWidth - m.Width - ((barWidth - m.Width) / 2))/* - (rowsCount) * 22*/;
						graphics.DrawString(text, font, new SolidBrush(textColor), new PointF(textLeft, offset));
						if (strikeout)
						{
							var height = 25;
							var width = 10;
							var top = (int)(offset + (m.Height / 2) - height / 2);// - height / 4);
							var left = (int)(textLeft);
							var right = (int)(textLeft + m.Width);
							var brush = new SolidBrush(Color.White);
							graphics.FillPolygon(brush, new[] { new Point(left + width, top), new Point(left + width, top + height), new Point(left, top + height), new Point(left + width, top) });
							graphics.DrawLine(new Pen(brush, height), left + width, top + height / 2, right, top);
							graphics.FillPolygon(brush, new[] { new Point(right, top - height / 2), new Point(right + width, top - height / 2), new Point(right, top + height - height / 2), new Point(right, top - height / 2) });
						}
						offset += (int)m.Height;
					}
				}
				File.WriteAllBytes(Path.Combine(folderCurrent, "sticker.png"), ImageResize.SaveAs(result, ImageFormat.Png, 100));
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
						graphics.DrawImage(iii, 0, 0, r.Width, r.Height);
					}
					var fileResult = Path.Combine(folderCurrent, DateTime.Now.Ticks.ToString() + ".png");
					File.WriteAllBytes(fileResult, ImageResize.SaveAs(r, ImageFormat.Png, 100));
					lock (stickerAddLock)
						try
						{
							var packName = "KashinChat_by_snapitbot";
							var emojis = File.ReadAllLines(Path.Combine(folderCurrent, "emojis.txt")).Select(s => s.Trim()).Distinct().Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
							var stickersCount = 0;
							try
							{
								var sstickers = Bot.GetStickerSetAsync(packName).Result.Stickers;
								if (sstickers.Length == 0)
									throw new Exception("sstickers.Length == 0");
								stickersCount = sstickers.Length;
								using (var fs = File.OpenRead(fileResult))
									Bot.AddStickerToSetAsync(148879395, packName, new InputOnlineFile(fs), emojis[stickersCount]).Wait();
							}
							catch (Exception ex)
							{
								Log.Error(ex);
								using (var fs = File.OpenRead(fileResult))
									Bot.CreateNewStickerSetAsync(148879395, packName, "Густопсовый", new InputOnlineFile(fs), emojis[stickersCount]).Wait();
							}
							fileId = Bot.GetStickerSetAsync(packName).Result.Stickers[Bot.GetStickerSetAsync(packName).Result.Stickers.Length - 1].FileId;
						}
						catch (Exception ex) { Log.Error(ex); }
					return fileResult;
				}
			}
		}

		public static void DrawRndRect(Graphics g, RectangleF r, Brush img, int roundRadius)
		{
			g.SmoothingMode = SmoothingMode.HighQuality;
			float X = r.Left;
			float Y = r.Top;
			if (roundRadius < 1)
				roundRadius = 1;
			using (GraphicsPath _Path = new GraphicsPath())
			{
				_Path.AddLine(X + roundRadius, Y, X + r.Width - (roundRadius * 2), Y);
				_Path.AddArc(X + r.Width - (roundRadius * 2), Y, roundRadius * 2, roundRadius * 2, 270, 90);
				_Path.AddLine(X + r.Width, Y + roundRadius, X + r.Width, Y + r.Height - (roundRadius * 2));
				_Path.AddArc(X + r.Width - (roundRadius * 2), Y + r.Height - (roundRadius * 2), roundRadius * 2, roundRadius * 2, 0, 90);
				_Path.AddLine(X + r.Width - (roundRadius * 2), Y + r.Height, X + roundRadius, Y + r.Height);
				_Path.AddArc(X, Y + r.Height - (roundRadius * 2), roundRadius * 2, roundRadius * 2, 90, 90);
				_Path.AddLine(X, Y + r.Height - (roundRadius * 2), X, Y + roundRadius);
				_Path.AddArc(X, Y, roundRadius * 2, roundRadius * 2, 180, 90);
				g.FillPath(img, _Path);
			}
		}

		static object stickerAddLock = new object[] { };
		private void StickerPack()
		{
			var folderCurrent = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			var emojis = File.ReadAllLines(Path.Combine(folderCurrent, "emojis.txt")).Select(s => s.Trim()).Distinct().Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
			var cites = File.ReadAllLines(Path.Combine(folderCurrent, "roizmangbn.txt"));//zayanymolchy
			var citesSel = cites.Where(s => !String.IsNullOrWhiteSpace(s)).Select(s => Regex.Replace(s.Trim(), @"\\n", "\n")).Distinct().ToArray();
			for (int i = 0; i < citesSel.Length; ++i)
			{
				var messageText = citesSel[i];
				var slotUrl = messageText;
				var foo = new PrivateFontCollection();
				foo.AddFontFile(Path.Combine(folderCurrent, "FreeSet-ExtraBoldOblique.otf"));
				var fontSize = 96f;
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
						//while (true)
						//{
						//	labelMeasure = graphics.MeasureString(slotUrl, font);
						//	if ((int)labelMeasure.Width <= (512 - 80))
						//	{
						//		labelMeasureStable = labelMeasure;
						//		fontSize++;
						//		font = new Font((FontFamily)foo.Families[0], fontSize);
						//	}
						//	else
						//	{
						//		fontSize--;
						//		font = new Font((FontFamily)foo.Families[0], fontSize);
						//		labelMeasure = graphics.MeasureString(slotUrl, font);
						//		break;
						//	}
						//}
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
						File.WriteAllBytes(Path.Combine(Directory.CreateDirectory(Path.Combine(folderCurrent, "!sticker_pack")).FullName, i.ToString("000") + "_s.png")
							, ImageResize.SaveAs(r, ImageFormat.Png, 100));
					}

					//if (slotUrl.Split('\n').Length > 1)
					//    using (var graphics = Graphics.FromImage(result))
					//    {
					//        SolidBrush brushSolid = new SolidBrush(backColor);
					//        graphics.Clear(Color.Transparent);
					//        graphics.CompositingQuality = CompositingQuality.HighQuality;
					//        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
					//        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
					//        graphics.SmoothingMode = SmoothingMode.HighQuality;
					//        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
					//        graphics.FillPolygon(brushSolid, new[] { new Point(40, 0), new Point(40, result.Height), new Point(0, result.Height), new Point(40, 0) });
					//        graphics.FillPolygon(brushSolid, new[] { new Point(result.Width - 1, 0), new Point(result.Width - 40, result.Height), new Point(result.Width - 40, 0), new Point(result.Width - 1, 0) });
					//        graphics.FillRectangle(brushSolid, 40, 0, result.Width - 80, result.Height);
					//        var isRoizman = messageText.StartsWith("/r", StringComparison.OrdinalIgnoreCase);
					//        {
					//            using (var imgRoizman = new Bitmap((int)labelMeasure.Height + (int)labelMeasure.Height / 3, (int)labelMeasure.Height + (int)labelMeasure.Height / 3, PixelFormat.Format24bppRgb))
					//            {
					//                imgRoizman.MakeTransparent();
					//                imgRoizman.SetResolution(96f, 96f);
					//                using (var img = Image.FromFile(Path.Combine(folderCurrent, "zayanymolchy.png")))
					//                using (var imgGraphics = Graphics.FromImage(imgRoizman))
					//                {
					//                    var scale = (float)img.Width / (float)imgRoizman.Width;
					//                    imgGraphics.Clear(ColorTranslator.FromHtml("#fff"));
					//                    imgGraphics.CompositingQuality = CompositingQuality.HighQuality;
					//                    imgGraphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
					//                    imgGraphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
					//                    imgGraphics.SmoothingMode = SmoothingMode.HighQuality;
					//                    imgGraphics.DrawImage(img, img.Width / 50, 0, imgRoizman.Height, imgRoizman.Height);
					//                }
					//                //                            File.WriteAllBytes(@"c:\Projects\MentionsTelegram\BigPicBotService\bin\Debug\!sticker_pack\" + i.ToString() + "_r.png"
					//                //, RCO.KFP.News.Utilities.ImageResize.SaveAs(imgRoizman, ImageFormat.Png, 100));
					//                DrawRndRect(graphics, new RectangleF(40, 10, (int)labelMeasure.Height, (int)labelMeasure.Height), new TextureBrush(imgRoizman), (int)labelMeasure.Height / 2);
					//            }
					//            graphics.DrawString(slotUrl, font, new SolidBrush(Color.White), new PointF((int)labelMeasure.Height + 80 / 2, 10));
					//        }
					//    }
					//else
					//    using (var graphics = Graphics.FromImage(result))
					//    {
					//        SolidBrush brushSolid = new SolidBrush(backColor);
					//        graphics.Clear(Color.Transparent);
					//        graphics.CompositingQuality = CompositingQuality.HighQuality;
					//        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
					//        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
					//        graphics.SmoothingMode = SmoothingMode.HighQuality;
					//        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
					//        graphics.FillPolygon(brushSolid, new[] { new Point(20, 0), new Point(20, result.Height), new Point(0, result.Height), new Point(20, 0) });
					//        graphics.FillPolygon(brushSolid, new[] { new Point(result.Width - 1, 0), new Point(result.Width - 20, result.Height), new Point(result.Width - 20, 0), new Point(result.Width - 1, 0) });
					//        graphics.FillRectangle(brushSolid, 20, 0, result.Width - 40, result.Height);
					//        var isRoizman = messageText.StartsWith("/r", StringComparison.OrdinalIgnoreCase);
					//        {
					//            using (var imgRoizman = new Bitmap((int)labelMeasure.Height + (int)labelMeasure.Height / 3, (int)labelMeasure.Height + (int)labelMeasure.Height / 3, PixelFormat.Format24bppRgb))
					//            {
					//                imgRoizman.MakeTransparent();
					//                imgRoizman.SetResolution(96f, 96f);
					//                using (var img = Image.FromFile(Path.Combine(folderCurrent, "zayanymolchy.png")))
					//                using (var imgGraphics = Graphics.FromImage(imgRoizman))
					//                {
					//                    var scale = (float)img.Width / (float)imgRoizman.Width;
					//                    imgGraphics.Clear(ColorTranslator.FromHtml("#fff"));
					//                    imgGraphics.CompositingQuality = CompositingQuality.HighQuality;
					//                    imgGraphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
					//                    imgGraphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
					//                    imgGraphics.SmoothingMode = SmoothingMode.HighQuality;
					//                    imgGraphics.DrawImage(img, -20, 0, imgRoizman.Height, imgRoizman.Height);
					//                }
					//                DrawRndRect(graphics, new RectangleF(20, 10, (int)labelMeasure.Height, (int)labelMeasure.Height), new TextureBrush(imgRoizman), (int)labelMeasure.Height / 2);
					//            }
					//            graphics.DrawString(slotUrl, font, new SolidBrush(Color.White), new PointF((int)labelMeasure.Height + 80 / 2, 10));
					//        }
					//    }
				}
			}
			var packName = "KashinRoizman_by_snapitbot";//zayanymolchy,KashinRoizman
			try
			{
				var stickers = Bot.GetStickerSetAsync(packName).Result.Stickers;
				foreach (var s in stickers)
				{
					while (true)
						try
						{
							Bot.DeleteStickerFromSetAsync(s.FileId).Wait();
							break;
						}
						catch
						{
							Thread.Sleep(4000);
						}
				}
			}
			catch { }
			var files = Directory.GetFiles(Directory.CreateDirectory(Path.Combine(folderCurrent, "!sticker_pack")).FullName, "*.png");
			//try
			//{
			//    using (var fs = File.OpenRead(files[0]))
			//        Bot.CreateNewStickerSetAsync(148879395, packName, "RCO", new InputOnlineFile(fs), emojis[0]).Wait();
			//    for (int i = 1; i < files.Length; ++i)
			//        while (true)
			//            try
			//            {
			//                using (var fs = File.OpenRead(files[i]))
			//                    Bot.AddStickerToSetAsync(148879395, packName, new InputOnlineFile(fs), emojis[i]).Wait();
			//                Thread.Sleep(1500);
			//                break;
			//            }
			//            catch (Exception ex)
			//            {
			//                Console.WriteLine(ex);
			//                Thread.Sleep(4000);
			//            }
			//}
			//catch { }
			for (int i = 0; i < files.Length; ++i)
				while (true)
					try
					{
						using (var fs = File.OpenRead(files[i]))
							Bot.AddStickerToSetAsync(148879395, packName, new InputOnlineFile(fs), emojis[i]).Wait();
						Thread.Sleep(1500);
						break;
					}
					catch (Exception ex)
					{
						Console.WriteLine(ex);
						Thread.Sleep(4000);
					}
			var sstickers = Bot.GetStickerSetAsync(packName).Result.Stickers;
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

		static readonly string[] SizeSuffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
		static string SizeSuffix(Int64 value, int decimalPlaces = 1)
		{
			if (decimalPlaces < 0) { throw new ArgumentOutOfRangeException("decimalPlaces"); }
			if (value < 0) { return "-" + SizeSuffix(-value); }
			if (value == 0) { return string.Format("{0:n" + decimalPlaces + "} bytes", 0); }

			// mag is 0 for bytes, 1 for KB, 2, for MB, etc.
			int mag = (int)Math.Log(value, 1024);

			// 1L << (mag * 10) == 2 ^ (10 * mag) 
			// [i.e. the number of bytes in the unit corresponding to mag]
			decimal adjustedSize = (decimal)value / (1L << (mag * 10));

			// make adjustment when the value is large enough that
			// it would round up to 1000 or more
			if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
			{
				mag += 1;
				adjustedSize /= 1024;
			}

			return string.Format("{0:n" + decimalPlaces + "} {1}",
				adjustedSize,
				SizeSuffixes[mag]);
		}

		private static ChromeOptions ChromeOptionsBase
		{
			get
			{
				var options = new ChromeOptions();
				options.BinaryLocation = BinaryLocation;
				options.AddArgument("headless");
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


		internal static bool ExecuteProcess(string fileExe, string args, TimeSpan waitSpan, out string[] consoleOK, out string[] consoleERR)
		{
			consoleOK = new string[0];
			consoleERR = new string[0];
			var outputErr = new List<string>();
			var outputStd = new List<string>();
			try
			{
				using (var process = new Process())
				{
					process.StartInfo = new ProcessStartInfo
					{
						FileName = fileExe,
						Arguments = args,
						UseShellExecute = false,
						RedirectStandardError = true,
						RedirectStandardOutput = true
					};
					using (var outputWaitHandle = new AutoResetEvent(false))
					using (var errorWaitHandle = new AutoResetEvent(false))
					{
						process.OutputDataReceived += (s, a) =>
						{
							if (a.Data == null)
								try { outputWaitHandle.Set(); } catch { }
							if (!string.IsNullOrWhiteSpace(a.Data))
								outputStd.Add(a.Data.Trim());
						};
						process.ErrorDataReceived += (s, a) =>
						{
							if (a.Data == null)
								try { errorWaitHandle.Set(); } catch { }
							if (!string.IsNullOrWhiteSpace(a.Data))
								outputErr.Add(a.Data.Trim());
						};
						KillProcess(process.StartInfo.FileName, process.StartInfo.Arguments);
						if (process.Start())
						{
							process.BeginOutputReadLine();
							process.BeginErrorReadLine();
							var timeout = waitSpan == null || waitSpan == TimeSpan.MaxValue ? -1 : (int)waitSpan.TotalMilliseconds;
							if (process.WaitForExit(timeout) && outputWaitHandle.WaitOne(timeout * 2) && errorWaitHandle.WaitOne(timeout * 2))
								return true;
							process.Kill();
							throw new TimeoutException("Process has timed out: " + waitSpan.TotalSeconds + " sec.");
						}
						throw new TimeoutException("Process \"" + fileExe + "\" hasn't started!");
					}
				}
			}
			catch (Exception ex)
			{
				outputErr.Add(ex.GetBaseException().ToString().Trim());
				return false;
			}
			finally
			{
				consoleOK = outputStd.ToArray();
				consoleERR = outputErr.ToArray();
				if (consoleERR.Length > 0)
					Log.Error(TruncateWhitespaces(String.Join(Environment.NewLine, consoleERR)));
				Log.Warn(fileExe + " " + args + "\tTimeout: " + waitSpan + "\tOK: " + String.Join(Environment.NewLine, consoleOK) + "\tERR: " + String.Join(Environment.NewLine, consoleERR));
			}
		}
		static string TruncateWhitespaces(string text)
		{
			return Regex.Replace(text.Trim().Replace("\t", "\\t").Replace("\r", "\\r").Replace("\n", "\\n"), @"\s+", " ").Replace("\\r\\n", "\\n").Replace("\\t", "  ").Trim();
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
		public static string Get(string originalLink, string shortLink, string folder, ChromeDriver driver)
		{
			string driverUrl;
			var fileImage = Path.Combine(folder, ComputeHash(shortLink) + ".png");
			if (File.Exists(fileImage) && new FileInfo(fileImage).Length > 0)
				return fileImage;
			int width = 0, height = 0, x = 0, y = 0;
			if (driver == null)
				throw new Exception("ChromeDriver is null!");
			driver.Navigate().GoToUrl(originalLink + (originalLink.ToLower().Contains("livejournal.com") ? "?embed=1" : string.Empty));
			try
			{
				int repeat = 300;
				while (!((IJavaScriptExecutor)driver).ExecuteScript("return document.readyState").Equals("complete") && repeat-- > 0)
					Thread.Sleep(100);
			}
			catch (Exception) { }

			driverUrl = driver.Url.ToLower();

			driver.GetScreenshot().SaveAsFile(fileImage + ".jpg", ScreenshotImageFormat.Jpeg);

			if (driverUrl.Contains("livejournal.com"))
				try { driver.Manage().Window.Size = new Size(500, driver.Manage().Window.Size.Height); }
				catch { }
			else
				driver.Manage().Window.Size = new Size(1920, 1080);
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

				try
				{
					var element = driver.FindElement(By.XPath("//*[contains(@aria-label,'Timeline: ') or contains(@aria-label,'Лента: ')]"));
					driver.ExecuteScript(@"
								var element = arguments[0];
								var deltaY = arguments[1];
								var box = element.getBoundingClientRect();
								var clientX = box.left + (arguments[2] || box.width / 2);
								var clientY = box.top + (arguments[3] || box.height / 2);
								var target = element.ownerDocument.elementFromPoint(clientX, clientY);
								for (var e = target; e; e = e.parentElement)
								{
									if (e === element)
									{
										target.dispatchEvent(new MouseEvent('mouseover', { view: window, bubbles: true, cancelable: true, clientX: clientX, clientY: clientY }));
										target.dispatchEvent(new MouseEvent('mousemove', { view: window, bubbles: true, cancelable: true, clientX: clientX, clientY: clientY }));
										target.dispatchEvent(new WheelEvent('wheel',     { view: window, bubbles: true, cancelable: true, clientX: clientX, clientY: clientY, deltaY: deltaY }));
										return;
									}
								}
								return ""Element is not interactable"";", element, -1500, 100, 100);
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex);
				}
				try
				{
					driver.ExecuteScript("window.scrollTo(0, 0);");
					//var element = driver.FindElement(By.XPath("//*[contains(@aria-label,'Timeline: ') or contains(@aria-label,'Лента: ')]"));
					//new OpenQA.Selenium.Interactions.Actions(driver).KeyDown(element, Keys.LeftShift).SendKeys(element, Keys.Space).Build().Perform();
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex);
				}
				Thread.Sleep(1000);
				var xpathes = new[] { By.XPath("//*[@data-testid='primaryColumn']/div/div[1]"), By.Id("layers"), By.Name("svg"), By.XPath("//*[@role='group']"), By.XPath("//*[@data-testid='caret']"), By.XPath("//*[@role='button']") };
				foreach (var xpath in xpathes)
					try
					{
						foreach (var elm in driver.FindElements(xpath))
							driverJS.ExecuteScript("arguments[0].remove();", elm);
					}
					catch { }
				Thread.Sleep(3000);
				var nodes = driver.FindElements(By.XPath("//*[contains(@aria-label,'Timeline: ') or contains(@aria-label,'Лента: ')]/div/div")).ToArray();
				var index = 0;
				for (; index < nodes.Length; ++index)
				{
					var divCount = nodes[index].FindElements(By.XPath(".//div")).Count;
					if (divCount < 10 && index > 0)
						break;
				}
				width = nodes[index].Size.Width - 10;
				height = int.Parse(Regex.Match(nodes[index].GetAttribute("style"), @"translateY\((\d+)px\);", RegexOptions.IgnoreCase).Groups[1].Value) - 20;
				x = 5;
				y = 5;
				if (height <= 0)
				{
					var eNext = nodes[index + 1];
					height = int.Parse(Regex.Match(eNext.GetAttribute("style"), @"translateY\((\d+)px\);", RegexOptions.IgnoreCase).Groups[1].Value) - 20;
				}
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
			File.WriteAllBytes(fileImage, SaveAs(Crop(fileImage, width, height, x, y, shortLink), ImageFormat.Png, 100));
			return fileImage;
		}

		static Bitmap GetEntireScreenshot(ChromeDriver browser)
		{
			var browserJSE = (IJavaScriptExecutor)browser;
			int totalWidth = (int)(long)browserJSE.ExecuteScript("return document.body.offsetWidth");
			int totalHeight = (int)(long)browserJSE.ExecuteScript("return  document.body.parentNode.scrollHeight");
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

		static MD5 hasher = MD5.Create();
		static readonly object hasherLock = new object[] { };
		public static string ComputeHash(string text)
		{
			return ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty));
		}

		public static string ComputeHash(byte[] textData)
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