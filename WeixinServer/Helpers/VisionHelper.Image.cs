﻿using Microsoft.ProjectOxford.Vision.Contract;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Configuration;
using System.Linq;
using WeixinServer.Models;
using System.Drawing.Drawing2D;
using Microsoft.ProjectOxford.Face.Contract;
namespace WeixinServer.Helpers
{
    public partial class VisionHelper
    {
        private string storageConnectionString;
        private CloudStorageAccount storageAccount;
        // Create the blob client.
        private CloudBlobClient blobClient = null;

        // Retrieve reference to a previously created container.
        private CloudBlobContainer container = null;

        private MemoryStream midStream;

        private void InitializePropertiesForAzure() 
        {
            //storageConnectionString = "DefaultEndpointsProtocol=https;AccountName=geeekstore;AccountKey=gwn9gAn+Uo6YqjdRNBF/mrM0Hbb54Ns61Rq9Q+ahhSyqrq64jrLMTn834cKmMKbqSFv9BTW8NtCFbUte49EzcA==";
            storageConnectionString = "DefaultEndpointsProtocol=https;AccountName=howhot;AccountKey=+teNObhdfANQ5/xkSLLH1cFIbF9q2kBdBZ98oBNO0K46xjcjAhuOrh47pHKbwdZZLVDrAG0wzKVtNgxbYDjg2w==";
            storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            blobClient = storageAccount.CreateCloudBlobClient();
            container = blobClient.GetContainerReference("cdn");
        }

        private void InitializePropertiesForImage(string frontImageUri)
        {
            this.frontImageUri = frontImageUri;
            
        }

        static Tuple<Font, float> FindFont(System.Drawing.Graphics g, string longString, Size Room, Font PreferedFont)
        {
            //you should perform some scale functions
            SizeF RealSize = g.MeasureString(longString, PreferedFont);
            float HeightScaleRatio = Room.Height / RealSize.Height;
            float WidthScaleRatio = Room.Width / RealSize.Width;
            float ScaleRatio = (HeightScaleRatio < WidthScaleRatio) ? ScaleRatio = HeightScaleRatio : ScaleRatio = WidthScaleRatio;
            float ScaleFontSize = PreferedFont.Size * ScaleRatio;
            //var intFontSize = ((int)ScaleFontSize / 4) * 4;
            //if (ScaleFontSize < 20) ScaleFontSize = 20;
            return new Tuple<Font, float>(new Font(PreferedFont.FontFamily, ScaleFontSize), ScaleFontSize);
        }



        static Image Resize(Image imgToResize, Size size)
        {
            Bitmap newImage = new Bitmap(size.Width, size.Height, imgToResize.PixelFormat);
            using (Graphics gr = Graphics.FromImage(newImage))
            {
                gr.SmoothingMode = SmoothingMode.HighQuality;
                gr.InterpolationMode = InterpolationMode.HighQualityBicubic;
                gr.PixelOffsetMode = PixelOffsetMode.HighQuality;
                gr.DrawImage(imgToResize, new System.Drawing.Rectangle(0, 0, size.Width, size.Height));
            }
            return (Image)newImage;
        }

        static double DistanceSquare(FeatureCoordinate p, FeatureCoordinate q)
        {
            return Math.Pow(p.X - q.X, 2) + Math.Pow(p.Y - q.Y, 2);
        }

        private MemoryStream DrawRects(MemoryStream inStream, AnalysisResult analysisResult) 
        {
            var faceDetections = analysisResult.RichFaces;
            //if(faceDetections == null || faceDetections.Length == 0) return null;
            int ascr = (int)(analysisResult.Adult.AdultScore * 2500);
            int rscr = (int)(analysisResult.Adult.RacyScore * 5000);
            int saoBility = ascr + rscr;
            //var hotivity = saoBility;
            Image image = Image.FromStream(inStream);
            var imageSize = image.Width;
            var rectEmsize = 1.0f;
            if (image.Height > maxNumPixs)
            {
                int height = maxNumPixs;
                int width = (int)((float)height * image.Width / image.Height);
                if (width < minNumPixs)
                {
                    width = minNumPixs;
                    height = (int)((float)width * image.Height / image.Width);
                }
                rectEmsize = (float) image.Height / height;
            }
            //        Watermark
            //var clr = new Microsoft.ProjectOxford.Vision.Contract.Color();
            //clr.AccentColor = "CAA501";
            var ms = new MemoryStream();
            // Modify the image using g

            //string fontName = "YourFont.ttf";
            PrivateFontCollection pfcoll = new PrivateFontCollection();
            //put a font file under a Fonts directory within your application root
            pfcoll.AddFontFile(this.frontImageUri);
            //pfcoll.AddFontFile(this.meoWuFontUri);

            FontFamily ff = pfcoll.Families[0];
            FontFamily ffMeo = pfcoll.Families[0];

            using (Graphics g = Graphics.FromImage(image))
            {
                var txtRectangles = new List<System.Drawing.Rectangle>();
                var femelRectangles = new List<System.Drawing.Rectangle>();
                var maleRectangles = new List<System.Drawing.Rectangle>();
                foreach (var faceDetect in faceDetections)
                {
                    if (rectEmsize > 0)
                    {
                        faceDetect.FaceRectangle.Top = (int)(faceDetect.FaceRectangle.Top * rectEmsize);
                        faceDetect.FaceRectangle.Left = (int)(faceDetect.FaceRectangle.Left * rectEmsize);
                        faceDetect.FaceRectangle.Width = (int)(faceDetect.FaceRectangle.Width * rectEmsize);
                        faceDetect.FaceRectangle.Height = (int)(faceDetect.FaceRectangle.Height * rectEmsize);
                    }

                    string genderInfo = "";

                    int topText = faceDetect.FaceRectangle.Top + faceDetect.FaceRectangle.Height + 5;
                    //int topText = faceDetect.FaceRectangle.Top - faceDetect.FaceRectangle.Height - 10;
                    topText = topText > 0 ? topText : 0;

                    var nickName = "哥";

                    var colour = System.Drawing.Color.Magenta;
                    if (faceDetect.Attributes.Gender.ToLower().Equals("male"))
                    {
                        //genderInfo += "♂";
                        //genderInfo += MaleTitleAsPerAge(faceDetect.Attributes.Age);
                        genderInfo = string.Format("♂{0}岁{1}\n", faceDetect.Attributes.Age, MaleTitleAsPerAge(faceDetect.Attributes.Age));
                        maleRectangles.Add(new System.Drawing.Rectangle(faceDetect.FaceRectangle.Left,
                            faceDetect.FaceRectangle.Top, faceDetect.FaceRectangle.Width, faceDetect.FaceRectangle.Height));
                        colour = System.Drawing.Color.Lime;
                        //maleRectangles.Add(new System.Drawing.Rectangle(leftText,
                        //    topText, faceDetect.FaceRectangle.Width, faceDetect.FaceRectangle.Top - topText));
                    }
                    else
                    {
                        //genderInfo += "♀";
                        genderInfo = string.Format("♀{0}岁{1}\n", faceDetect.Attributes.Age, FemaleTitleAsPerAge(faceDetect.Attributes.Age));
                        //genderInfo += FemaleTitleAsPerAge(faceDetect.Attributes.Age);
                        femelRectangles.Add(new System.Drawing.Rectangle(faceDetect.FaceRectangle.Left,
                            faceDetect.FaceRectangle.Top, faceDetect.FaceRectangle.Width, faceDetect.FaceRectangle.Height));
                        nickName = "妹";
                        //femelRectangles.Add(new System.Drawing.Rectangle(leftText,
                        //    topText, faceDetect.FaceRectangle.Width, faceDetect.FaceRectangle.Top - topText));
                    }
                    //draw text 
                    var hotivity = saoBility / faceDetect.Attributes.Age;
                    
                    //var hotivity = 100000 * (faceDetect.FaceRectangle.Height / faceDetect.FaceRectangle.Width - 1);
                    var lm = faceDetect.FaceLandmarks;
                    var emLargeRate = DistanceSquare(lm.EyeLeftBottom, lm.EyeLeftTop) / DistanceSquare(lm.EyeLeftInner, lm.EyeLeftOuter);
                    //var mouthLargeRate = DistanceSquare(lm.UpperLipBottom, lm.UnderLipTop) / DistanceSquare(lm.EyeLeftInner, lm.EyeRightInner);
                    
                    var eyeDescribion = "";
                    if (emLargeRate > 0.15)
                    {
                        eyeDescribion = "铜铃眼";

                    }
                    else if (emLargeRate < 0.08)
                    {
                        eyeDescribion = "丹凤眼";
                    }
                    else 
                    {
                        eyeDescribion = "桃花眼";
                    }
                    genderInfo += eyeDescribion;
                    string mouthDescription = "";
                    var mouthLargeRate = DistanceSquare(lm.MouthRight, lm.MouthLeft) / DistanceSquare(lm.EyeLeftInner, lm.EyeRightInner);
                    if (mouthLargeRate > 1.6)
                    {
                        mouthDescription = "姚晨吻";

                    }
                    else if (mouthLargeRate < 1.2)
                    {
                        mouthDescription = "舒淇唇";
                    }
                    else
                    {
                        mouthDescription = "姚明嘴";
                        //eyeDescribion += "\n性感红唇";
                    }

                    hotivity += emLargeRate * 100;
                    //hotivity = hotivity / 10
                    string info = string.Format("{0}\n{1:F0}分\n", mouthDescription, hotivity);
                    
                    Size room = new Size((int) (faceDetect.FaceRectangle.Width) , (int)(faceDetect.FaceRectangle.Height));
                    var ret = FindFont(g, info, room, new Font(ff, 36, FontStyle.Bold, GraphicsUnit.Pixel));
                    var fontSize = ret.Item2;
                    Font f = new Font(ff, fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
                    //int leftText = faceDetect.FaceRectangle.Left - faceDetect.FaceRectangle.Width;
                    int leftText = faceDetect.FaceRectangle.Left;
                    leftText = leftText > 0 ? leftText : 0;
                   // g.DrawString(info, f, new SolidBrush(colour), new Point(leftText, topText));

                    var fHead = new Font(ffMeo, (int)(fontSize * 1.3), FontStyle.Bold, GraphicsUnit.Pixel);
                    //g.DrawString(string.Format("{0}{1}", genderInfo, faceDetect.Attributes.Age), fHead, new SolidBrush(colour),
                    //    new Point(faceDetect.FaceRectangle.Left, faceDetect.FaceRectangle.Top - f.Height - 5));
                    //some test image for this demo
                    Bitmap bmp = (Bitmap)image;
                    // Graphics g = Graphics.FromImage(bmp);

                    //this will center align our text at the bottom of the image
                    StringFormat sf = new StringFormat();
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Far;

                    //define a font to use.
                    f = new Font(ff, fontSize, FontStyle.Bold, GraphicsUnit.Pixel);

                    //pen for outline - set width parameter
                    //Pen p = new Pen(ColorTranslator.FromHtml("#77090C"), 8);
                    //Pen p = new Pen(fontColor, 8);
                    Pen p = new Pen(System.Drawing.Color.White, 8);

                    p.LineJoin = LineJoin.Round; //prevent "spikes" at the path

                    //this makes the gradient repeat for each text line
                    System.Drawing.Rectangle fr = new System.Drawing.Rectangle(faceDetect.FaceRectangle.Left,
                        faceDetect.FaceRectangle.Top, faceDetect.FaceRectangle.Width * 2, f.Height);

                    LinearGradientBrush b = new LinearGradientBrush(fr,
                                                                    System.Drawing.Color.Aqua,
                                                                    System.Drawing.Color.DodgerBlue,
                                                                    ////ColorTranslator.FromHtml("#FF6493"),
                                                                    ////ColorTranslator.FromHtml("#D00F14"),
                                                                    90);           //// the upper text 

                    //this makes the gradient repeat for each text line
                    System.Drawing.Rectangle fr2 = new System.Drawing.Rectangle(faceDetect.FaceRectangle.Left,
                        faceDetect.FaceRectangle.Top, faceDetect.FaceRectangle.Width * 2, f.Height);

                    LinearGradientBrush b2 = new LinearGradientBrush(fr2,
                                                                    System.Drawing.Color.Aqua,
                                                                    System.Drawing.Color.DodgerBlue,
                                                                    90);
                    var genderTop = faceDetect.FaceRectangle.Top - (int)(f.Height * 3);

                    genderTop = genderTop > f.Height ? genderTop : (int)(f.Height * 0.618);
                    int genderHeight = faceDetect.FaceRectangle.Top - genderTop;
                    room = new Size((int)(faceDetect.FaceRectangle.Width), genderHeight);
                    ret = FindFont(g, info, room, new Font(ff, 36, FontStyle.Bold, GraphicsUnit.Pixel));
                    var headTextFontSize = ret.Item2;
                    System.Drawing.Rectangle r2 = new System.Drawing.Rectangle(faceDetect.FaceRectangle.Left,
                           genderTop,
                           faceDetect.FaceRectangle.Width,
                           genderHeight);
                    //this will be the rectangle used to draw and auto-wrap the text.
                    //basically = image size
                    int width = faceDetect.FaceRectangle.Width;
                    if (width + faceDetect.FaceRectangle.Left > image.Width) width = image.Width - faceDetect.FaceRectangle.Left - width;
                    int height = (int)(0.618 * faceDetect.FaceRectangle.Height);
                    if (2 * height + faceDetect.FaceRectangle.Top > image.Height) height = (int)(0.618 * (image.Height - faceDetect.FaceRectangle.Top) - height);
                    System.Drawing.Rectangle r = new System.Drawing.Rectangle(faceDetect.FaceRectangle.Left,
                            faceDetect.FaceRectangle.Top + (int)(faceDetect.FaceRectangle.Height * 1.1),
                            width,
                            height);

                    GraphicsPath gp = new GraphicsPath();

                    //look mom! no pre-wrapping!
                    gp.AddString(info, ff, (int)FontStyle.Bold, fontSize, r, sf);
                    //string.Format("{0}{1}", genderInfo, faceDetect.Attributes.Age)
                    gp.AddString(genderInfo, ff, (int)FontStyle.Bold, headTextFontSize, r2, sf);
                    //gp.DrawString(info, f, new SolidBrush(colour), new Point(leftText, topText));
                    //gp.AddString(string.Format("{0}{1}", genderInfo, faceDetect.Attributes.Age), ff, (int)FontStyle.Bold, fontSize, r, sf);
                    //    new Point(faceDetect.FaceRectangle.Left, faceDetect.FaceRectangle.Top - f.Height - 5));
                    
                    //these affect lines such as those in paths. Textrenderhint doesn't affect
                    //text in a path as it is converted to ..well, a path.    
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                    //TODO: shadow -> g.translate, fillpath once, remove translate
                    g.DrawPath(p, gp);
                    g.FillPath(b2, gp);

                    //cleanup
                    gp.Dispose();
                    b.Dispose();
                    b.Dispose();
                    f.Dispose();
                    sf.Dispose();

                    //layers.Add(this.GetFaceTextLayer(info, leftText, topText, clr));

                }       //end of for

                // Create a brush with an alpha value and use the g.FillRectangle function
                //var customColor = System.Drawing.Color.FromArgb(255, System.Drawing.Color.Gray);
                //TextureBrush shadowBrush = new TextureBrush(image);
                if (femelRectangles.Count > 0)
                {
                    Pen pen = new Pen(System.Drawing.Color.Magenta, 2);
                    g.DrawRectangles(pen, femelRectangles.ToArray());
                }
                if (maleRectangles.Count > 0)
                {
                    Pen pen2 = new Pen(System.Drawing.Color.Lime, 2);
                    g.DrawRectangles(pen2, maleRectangles.ToArray());
                }

                image.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
            }

            
            
            return ms;
            //timeLogger.Append(string.Format("{0} VisionHelper::AnalyzeImage::RenderAnalysisResultAsImage imageFactory.Load begin\n", DateTime.Now - this.startTime));
            //using (var imageFactory = new ImageFactory(preserveExifData: false))
            //{
            //    imageFactory.Load(ms);
            //    foreach(var layer in layers)
            //    {
            //        imageFactory.Watermark(layer);
                                
            //    }
            //    imageFactory.Format(new JpegFormat()).Save(outStream);
            //}
            //timeLogger.Append(string.Format("{0} VisionHelper::AnalyzeImage::RenderAnalysisResultAsImage imageFactory.Load midStream generated\n", DateTime.Now - this.startTime));
            //return outStream;
        }

        private MemoryStream tulisnamafile(MemoryStream imageMS, string textnya)
        {

            Image image = Image.FromStream(imageMS);
            //Bitmap newImage = new Bitmap(640, 380);
            MemoryStream ms = new MemoryStream();
            using (Graphics g = Graphics.FromImage(image))
            {
                // Draw base image
                g.DrawImageUnscaled(image, 0, 0);
                //Static is HERE
                SolidBrush brushing = new SolidBrush(System.Drawing.Color.White);
                Font font = new Font(("Comic Sans MS"), 20.0f);
                int napoint = image.Height - 90;
                int napointa = image.Width - 200;
                //FontFamily ff = new FontFamily("Times New Roman");
                PrivateFontCollection pfcoll = new PrivateFontCollection();
                //put a font file under a Fonts directory within your application root
                pfcoll.AddFontFile(this.frontImageUri);
                pfcoll.AddFontFile(this.meoWuFontUri);
                FontFamily ff = pfcoll.Families[0];
                int fontSize = 36;
                Font f = new Font(ff, fontSize, FontStyle.Regular);
                StringFormat sf = new StringFormat();
                System.Drawing.Rectangle displayRectangle = 
                    new System.Drawing.Rectangle(new Point(5, napoint), new Size(image.Width - 1, image.Height - 1));
                g.DrawEllipse(Pens.Magenta, new System.Drawing.Rectangle(0, 0, 1, 1));
                GraphicsPath gp = new GraphicsPath();
                gp.AddString(textnya, ff, (int)FontStyle.Bold, fontSize + 4, new Point(0, 0), sf);
                g.FillPath(Brushes.White, gp);
                g.DrawPath(Pens.Black, gp);

                g.Flush(FlushIntention.Sync);
                g.Dispose();
            }
            
            image.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
            image.Dispose();
            return ms;
        }

        private MemoryStream tulisnamafile2(MemoryStream imageMS, string textnya)
        {

            float fontSize = 22;

            Image image = Image.FromStream(imageMS);
            MemoryStream ms = new MemoryStream();

            //some test image for this demo
            Bitmap bmp = (Bitmap)image;
            Graphics g = Graphics.FromImage(bmp);

            //this will center align our text at the bottom of the image
            StringFormat sf = new StringFormat();
            sf.Alignment = StringAlignment.Center;
            sf.LineAlignment = StringAlignment.Far;

            //define a font to use.
            Font f = new Font("Impact", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);

            //pen for outline - set width parameter
            Pen p = new Pen(ColorTranslator.FromHtml("#77090C"), 8);
            p.LineJoin = LineJoin.Round; //prevent "spikes" at the path

            //this makes the gradient repeat for each text line
            System.Drawing.Rectangle fr = new System.Drawing.Rectangle(0, bmp.Height - f.Height, bmp.Width, f.Height);
            LinearGradientBrush b = new LinearGradientBrush(fr,
                                                            ColorTranslator.FromHtml("#FF6493"),
                                                            ColorTranslator.FromHtml("#D00F14"),
                                                            90);

            //this will be the rectangle used to draw and auto-wrap the text.
            //basically = image size
            System.Drawing.Rectangle r = new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height);

            GraphicsPath gp = new GraphicsPath();

            //look mom! no pre-wrapping!
            gp.AddString(textnya, f.FontFamily, (int)FontStyle.Bold, fontSize, r, sf);

            //these affect lines such as those in paths. Textrenderhint doesn't affect
            //text in a path as it is converted to ..well, a path.    
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            //TODO: shadow -> g.translate, fillpath once, remove translate
            g.DrawPath(p, gp);
            g.FillPath(b, gp);

            //cleanup
            gp.Dispose();
            b.Dispose();
            b.Dispose();
            f.Dispose();
            sf.Dispose();
            g.Dispose();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
            bmp.Dispose();
            return ms;

        }

        private MemoryStream DrawText(string text, int width, int leftOrRight, Microsoft.ProjectOxford.Vision.Contract.Color color)
        {
            const int RGBMAX = 255;
            PrivateFontCollection pfcoll = new PrivateFontCollection();
            //put a font file under a Fonts directory within your application root
            pfcoll.AddFontFile(this.frontImageUri);
            FontFamily ff = pfcoll.Families[0];
            System.Drawing.Color fontColor = System.Drawing.Color.DeepPink;
            if (color != null && !string.IsNullOrWhiteSpace(color.AccentColor))
            {
                var accentColor = ColorTranslator.FromHtml("#" + color.AccentColor);
                fontColor = System.Drawing.Color.FromArgb(RGBMAX - accentColor.R, RGBMAX - accentColor.G, RGBMAX - accentColor.B);
            }

            var ms = new MemoryStream();

            Image image = Image.FromStream(midStream);
            
            using (Graphics g = Graphics.FromImage(image))
            {
                var fontSize = width < 1000 ? 24 : 36;
                var ret = FindFont(g, text, new Size(image.Width , image.Height), new Font(ff, fontSize, FontStyle.Bold, GraphicsUnit.Pixel));
                fontSize = (int)ret.Item2;
                int count = 1;
                int start = 0;
                while ((start = text.IndexOf('\n', start)) != -1)
                {
                    count++;
                    start++;
                }
                
                Font f = new Font(ff, fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
                var x = (int)(image.Width * 0.05);
                var y = image.Height - (fontSize + 5) * count;

                //g.DrawString(text, f, new SolidBrush(fontColor), new Point(x, y));
                
                
                //Draw better sytle

                //some test image for this demo
                Bitmap bmp = (Bitmap)image;
               // Graphics g = Graphics.FromImage(bmp);

                //this will center align our text at the bottom of the image
                StringFormat sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Far;

                //define a font to use.
                f = new Font(ff, fontSize, FontStyle.Bold, GraphicsUnit.Pixel);

                //pen for outline - set width parameter
                //Pen p = new Pen(ColorTranslator.FromHtml("#77090C"), 8);
                //Pen p = new Pen(fontColor, 8);
                //Pen p = new Pen(ColorTranslator.FromHtml("#777777"), 8);
                Pen p = new Pen(System.Drawing.Color.White, 8);
                
                p.LineJoin = LineJoin.Round; //prevent "spikes" at the path

                //this makes the gradient repeat for each text line
                System.Drawing.Rectangle fr = new System.Drawing.Rectangle(0, bmp.Height - 2 * f.Height, bmp.Width, f.Height);
                ////LinearGradientBrush b = new LinearGradientBrush(fr,
                ////                                                ColorTranslator.FromHtml("#FF6493"),
                ////                                                ColorTranslator.FromHtml("#D00F14"),
                ////                                                90);

                LinearGradientBrush b = new LinearGradientBrush(fr,
                                                System.Drawing.Color.Aqua,
                                                System.Drawing.Color.DodgerBlue,
                                                90);           //// the upper text 
                //this will be the rectangle used to draw and auto-wrap the text.
                //basically = image size
                System.Drawing.Rectangle r;
                switch (leftOrRight)
                { case 0:     //leftTop
                        r = new System.Drawing.Rectangle(0, 0, bmp.Width / 4, bmp.Height / 8);
                        break;
                  case 1:     //rightTop
                        r = new System.Drawing.Rectangle(bmp.Width * 3 / 4, 0, bmp.Width / 4, bmp.Height / 8);
                        break;
                  
                 case 2:            //leftBottom
                        r = new System.Drawing.Rectangle(0, bmp.Height * 7 / 8, bmp.Width / 4, bmp.Height / 8);
                        break;
                 case 3:            //rightBottom
                        r = new System.Drawing.Rectangle(bmp.Width * 3 / 4, bmp.Height * 7 / 8, bmp.Width / 4, bmp.Height / 8);
                        break;
                 case 4:            //lowerCenter
                        r = new System.Drawing.Rectangle(bmp.Width * 1 / 5, bmp.Height * 3 / 5, bmp.Width * 3 / 5, bmp.Height * 3 / 5);
                        break;
                 default:
                        r = new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height);
                        break;
                }
                //if(leftOrRight == 0)
                //    r = new System.Drawing.Rectangle(0, 0, bmp.Width / 4, bmp.Height / 8);
                //else
                //    r = new System.Drawing.Rectangle(bmp.Width * 2 / 3, 0, bmp.Width / 3, bmp.Height);
                GraphicsPath gp = new GraphicsPath();

                //look mom! no pre-wrapping!
                gp.AddString(text, ff, (int)FontStyle.Bold, fontSize, r, sf);

                //these affect lines such as those in paths. Textrenderhint doesn't affect
                //text in a path as it is converted to ..well, a path.    
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                //TODO: shadow -> g.translate, fillpath once, remove translate
                g.DrawPath(p, gp);
                g.FillPath(b, gp);

                //cleanup
                gp.Dispose();
                b.Dispose();
                b.Dispose();
                f.Dispose();
                sf.Dispose();
                g.Dispose();


                image.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
            }

            return ms;
        }

        private static string random_string(int length = 12)
        {
            var chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            return new string(
                Enumerable.Repeat(chars, length)
                          .Select(s => s[random.Next(s.Length)])
                          .ToArray());

        }
        private string RenderAnalysisResultAsImage(AnalysisResult result, string captionText, string commentText)
        {
            timeLogger.Append(string.Format("{0} VisionHelper::AnalyzeImage::RenderAnalysisResultAsImage begin\n", DateTime.Now - this.startTime));
            string resultUrl = null;

            var upyun = new UpYun("wxhowoldtest", "work01", "vYiJVc7iYY33w58O");

            //using (var midStream = new MemoryStream(photoBytes))
            //{
                midStream = new MemoryStream(photoBytes);
                var outStream = new MemoryStream();
                timeLogger.Append(string.Format("{0} VisionHelper::AnalyzeImage::RenderAnalysisResultAsImage imageFactory.Load begin\n", DateTime.Now - this.startTime));

                midStream.Seek(0, SeekOrigin.Begin);
                midStream = DrawText(captionText, result.Metadata.Width, 5, result.Color);
                midStream.Seek(0, SeekOrigin.Begin);

                midStream = DrawRects(midStream, result);
                midStream.Seek(0, SeekOrigin.Begin);

                //var len = commentText.Length;
                //midStream = DrawText(commentText.Substring(0, len / 4), result.Metadata.Width, 0, result.Color);
                //midStream.Seek(0, SeekOrigin.Begin);
                //midStream = DrawText(commentText.Substring(len / 4, len / 4), result.Metadata.Width, 1, result.Color);
                //midStream.Seek(0, SeekOrigin.Begin);
                //midStream = DrawText(commentText.Substring(2 * (len / 4), len / 4), result.Metadata.Width, 2, result.Color);
                //midStream.Seek(0, SeekOrigin.Begin);
                //midStream = DrawText(commentText.Substring(3 * (len / 4)), result.Metadata.Width, 3, result.Color);
                //midStream.Seek(0, SeekOrigin.Begin);
                
                //outStream.Seek(0, SeekOrigin.Begin);

                //var midStream = 
                timeLogger.Append(string.Format("{0} VisionHelper::AnalyzeImage::RenderAnalysisResultAsImage imageFactory.Load midStream generated\n", DateTime.Now - this.startTime));

                
                //outStream.Seek(0, SeekOrigin.Begin);
                //outStream = tulisnamafile2(midStream, captionText);
                timeLogger.Append(string.Format("{0} VisionHelper::AnalyzeImage::RenderAnalysisResultAsImage Upload to image CDN begin\n", DateTime.Now - this.startTime));
                    
                //   return string.Format("酷评:\n{0}\n归图:\n{1}", captionText, resultUrl);
                    

                // Retrieve reference to a blob named "myblob".
                //string blobName = string.Format("{0}_{1}.jpg", this.curUserName, random_string(12));
                int idx = this.curUserName.LastIndexOf('_');
                idx = idx > -1? idx : 0;
                string blobName = string.Format("{0}/{1}.jpg", this.curUserName.Substring(idx), random_string(13));
                CloudBlockBlob blockBlob = container.GetBlockBlobReference(blobName);

                // Create or overwrite the "myblob" blob with contents from a local file.
                //midStream.Seek(0, SeekOrigin.Begin);
                //blockBlob.UploadFromStream(midStream);
                midStream.Seek(0, SeekOrigin.Begin);
                blockBlob.UploadFromStream(midStream);
                resultUrl = "http://howhot.blob.core.windows.net/cdn/" + blobName;
                //resultUrl = "http://geeekstore.blob.core.windows.net/cdn/" + blobName;
                //resultUrl = upyun.UploadImageStream(outStream);

                this.returnImageUrl = resultUrl;                    
                timeLogger.Append(string.Format("{0} VisionHelper::AnalyzeImage::RenderAnalysisResultAsImage Upload to image CDN end\n", DateTime.Now - this.startTime));
            //}


            //return string.Format("画说:\n{0}", resultUrl);
                //return string.Format("谈画:\n{0}\n归图:\n{1}\n", noAdsTxtResult, resultUrl);
                //return string.Format("谈画:\n{0}\n想知道您上传的图片有多\"Hot\"么? 请看归图:\n{1}\n", commentText, resultUrl);
                return string.Format("谈画:\n{0}", commentText);
            //return string.Format("画说:\n{0}\n归图:\n{1}\n原图:\n{2}", captionText, resultUrl, this.originalImageUrl);
        }
    }
}
