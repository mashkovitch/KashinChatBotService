namespace KashinChatBotService
{
	using ImageProcessor;
	using ImageProcessor.Plugins.WebP.Imaging.Formats;
	using Newtonsoft.Json;
	using NLog;
	using System;
	using System.Collections.Generic;
	using System.Configuration;
	using System.Drawing;
	using System.Drawing.Drawing2D;
	using System.Drawing.Imaging;
	using System.Drawing.Text;
	using System.IO;
	using System.Linq;
	using System.Reflection;
	using System.Text.RegularExpressions;
	using System.Threading;
	using Telegram.Bot;
	using Telegram.Bot.Args;
	using Telegram.Bot.Types.Enums;
	using Telegram.Bot.Types.InputFiles;
	using Topshelf;
	using WikipediaNet;
	using WikipediaNet.Enums;

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
	}
}