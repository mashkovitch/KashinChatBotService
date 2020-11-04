namespace KashinChatBotService
{
	using System;
	using System.Drawing;
	using System.Drawing.Drawing2D;
	using System.Drawing.Imaging;
	using System.IO;
    
    class ImageResize
	{
		internal static byte[] Resize(Image image, int? MaxSize, int? MaxWidth, int? MaxHeight, int? CanvasWidth, int? CanvasHeight, bool roundNeeded)
		{
			var data = new byte[0];
			using (image)
				data = SaveAs(image, ImageFormat.Png, 100);
			return Resize(data, MaxSize, MaxWidth, MaxHeight, CanvasWidth, CanvasHeight, roundNeeded);
		}

		internal static byte[] Resize(byte[] imageData, int? MaxSize, int? MaxWidth, int? MaxHeight, int? CanvasWidth, int? CanvasHeight, bool roundNeeded)
		{
			int imageWidth;
			int imageHeight;
			using (var ms = new MemoryStream(imageData))
			using (var image = Image.FromStream(ms))
			{
				imageWidth = image.Width;
				imageHeight = image.Height;
			}
			if (imageWidth <= 0 || imageHeight <= 0)
				throw new ArgumentOutOfRangeException("Высота и ширина "
					+ "изображения не могут быть меньше или равны нулю.");
			int resultWidth, resultHeight;
			if (!MaxWidth.HasValue && !MaxHeight.HasValue)
			{
				resultWidth = imageWidth;
				resultHeight = imageHeight;
			}
			else
			{
				if (imageHeight > imageWidth)//если изображение вертикальное обрезаем его до квадрата по ширине
				{
					var rectCrop = new Rectangle { X = 0, Y = 0, Width = imageWidth, Height = imageWidth };
					using (var ms = new MemoryStream(imageData))
					using (var image = (Bitmap)Image.FromStream(ms))
					using (var crop = image.Clone(rectCrop, image.PixelFormat))
					{
						imageWidth = image.Width;
						imageHeight = image.Height;
						imageData = SaveAs(crop, ImageFormat.Png, 100);
					}
				}
				MaxSize = Math.Max(MaxSize ?? 0, Math.Max(MaxWidth ?? 0, MaxHeight ?? 0));
				float scaleHeight = (float)MaxSize / (float)imageHeight;
				float scaleWidth = (float)MaxSize / (float)imageWidth;
				float scale = Math.Min(scaleHeight, scaleWidth);
				resultWidth = (int)(imageWidth * scale);
				resultHeight = (int)(imageHeight * scale);
				if (MaxWidth.HasValue && MaxWidth.Value > 0 && MaxHeight.HasValue && MaxHeight.Value > 0)
				{
					CanvasWidth = CanvasWidth ?? MaxWidth ?? MaxSize;
					CanvasHeight = CanvasHeight ?? MaxHeight ?? MaxSize;
				}
			}
			//ресайзим под нужный размер, если картинка крупнее необходимого
			if (resultHeight < imageHeight || resultWidth < imageWidth)
				using (var ms = new MemoryStream(imageData))
				using (var image = (Bitmap)Image.FromStream(ms))
				using (Bitmap result = new Bitmap(resultWidth, resultHeight, image.PixelFormat))
				{
					result.MakeTransparent();
					using (Graphics graphics = Graphics.FromImage(result))
					{
						graphics.SmoothingMode = SmoothingMode.HighQuality;
						graphics.CompositingQuality = CompositingQuality.HighQuality;
						graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
						graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
						graphics.DrawImage(image, 0, 0, resultWidth, resultHeight);
					}
					imageData = SaveAs(result, ImageFormat.Png, 100);
				}
			//закругляем углы, если нужно
			if (roundNeeded)
				using (var ms = new MemoryStream(imageData))
				using (var image = (Bitmap)Image.FromStream(ms))
				using (Bitmap result = new Bitmap(image.Width, image.Height, image.PixelFormat))
				{
					result.MakeTransparent();
					using (Graphics roundedGraphics = Graphics.FromImage(result))
					{
						RectangleF r = new RectangleF(0, 0, image.Width, image.Height);
						DrawRndRect(roundedGraphics, r, new TextureBrush(image), 12);
					}
					imageData = SaveAs(result, ImageFormat.Png, 100);
				}
			//добавляем поля, если необходимо
			if (CanvasWidth.HasValue && CanvasHeight.HasValue)
				using (var ms = new MemoryStream(imageData))
				using (var image = (Bitmap)Image.FromStream(ms))
				using (Bitmap result = new Bitmap(CanvasWidth.Value, CanvasHeight.Value, image.PixelFormat))
					if (image.Height < CanvasHeight || image.Width < CanvasWidth)
					{
						result.MakeTransparent();
						using (var graphics = Graphics.FromImage(result))
						{
							graphics.CompositingQuality = CompositingQuality.HighQuality;
							graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
							graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
							RectangleF d = new RectangleF((float)((float)(result.Width - image.Width) / 2.0)
								, (float)((float)(result.Height - image.Height) / 2.0)
								, image.Width
								, image.Height
							);
							RectangleF r = new RectangleF(0, 0, image.Width, image.Height);
							graphics.DrawImage(image, d, r, GraphicsUnit.Pixel);
						}
						imageData = SaveAs(result, ImageFormat.Png, 100);
					}
			return imageData;
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

		public static byte[] SaveAs(Image image, ImageFormat imageFormat, Int64 quality)
		{
			ImageCodecInfo[] imageCodecs = ImageCodecInfo.GetImageDecoders();
			ImageCodecInfo formatEncoder = Array.Find<ImageCodecInfo>(imageCodecs, (c => c.FormatID == imageFormat.Guid));
			if (formatEncoder == null)
				formatEncoder = Array.Find<ImageCodecInfo>(imageCodecs, (c => c.FormatID == ImageFormat.Png.Guid));
			using (var qualityEncoderParameter = new EncoderParameter(Encoder.Quality,quality))
			using (var encoderParameters = new EncoderParameters(1))
			using (var ms = new MemoryStream())
			{
				encoderParameters.Param[0] = qualityEncoderParameter;
                image.Save(ms, formatEncoder, encoderParameters);
                return ms.ToArray();
			}
		}
	}
}