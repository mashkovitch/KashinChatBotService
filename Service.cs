namespace KashinChatBotService
{
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
		static readonly string[] videos = "DQACAgIAAxkDAAIEfl-hWGkIGlt9ox_aV5HCHoFzu6uLAAMJAAIKYQlJB2yyvqNHbAkeBA,DQACAgIAAxkDAAIEf1-hWG0NZKLp6QTGD-u-N0pB3C7dAAIBCQACCmEJSZZ3aBCNxEZiHgQ,DQACAgIAAxkDAAIEgF-hWG4yLT13doQC3ITCPosStLOOAAICCQACCmEJSTobDAOuB2-jHgQ,DQACAgIAAxkDAAIEgV-hWG4kS9Qd1On9gSa-oZ-ab3yXAAIDCQACCmEJSe51bd9iyaVEHgQ,DQACAgIAAxkDAAIEgl-hWG9D3WHMnhMTboOR6Ez_jd4XAAIECQACCmEJSX4HDOmNplicHgQ,DQACAgIAAxkDAAIEg1-hWHC4g6mPH7m1V6XwmEVCp1pAAAIFCQACCmEJSR_xnUupGljKHgQ".Split(',');
		public bool Start(HostControl hostControl)
		{
			Log = GetLogger();
			//StickerPack();
			ProcessMessages();
			return true;
		}

		private void ProcessMessages()
		{
			Console.WriteLine(Bot.GetMeAsync().Result);
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
								//using (var ms = new MemoryStream())
								//{
								//	using (var img = Image.FromFile(CreateSticker(string.IsNullOrWhiteSpace(messageText) ? "РУССКИЕ\nВПЕРЕД!" : messageText)))
								//	using (var imageFactory = new ImageFactory(preserveExifData: false))
								//		imageFactory.Load(img).Format(new WebPFormat()).Quality(100).Save(ms);
								//	Bot.SendStickerAsync(message.Chat.Id, new InputOnlineFile(ms)).Wait();
								//}
								using (var fs = File.OpenRead(CreateSticker(string.IsNullOrWhiteSpace(messageText) ? "РУССКИЕ\nВПЕРЕД!" : messageText)))
									Bot.SendStickerAsync(message.Chat.Id, new InputOnlineFile(fs)).Wait();
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
			//var angle = 10;
			//var b = (int)Math.Abs(((90 - angle) * Math.Tan(angle))) * rowsCount;
			var b = (int)Math.Ceiling((labelMeasure.Height * 30) / 90);
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
							textLeft += (int)((barWidth - m.Width) - (barWidth - m.Width) / 2) - (rowsCount) * 22;
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
					return fileResult;
				}
			}
		}

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