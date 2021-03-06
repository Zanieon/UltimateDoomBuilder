
#region ================== Copyright (c) 2007 Pascal vd Heiden

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 */

#endregion

#region ================== Namespaces

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using CodeImp.DoomBuilder.Rendering;
using CodeImp.DoomBuilder.IO;
using System.IO;
using System.Linq;

#endregion

namespace CodeImp.DoomBuilder.Data
{
	internal sealed unsafe class TextureImage : ImageData
	{
		#region ================== Variables

		private List<TexturePatch> patches;
		
		#endregion

		#region ================== Constructor / Disposer

		// Constructor
		public TextureImage(string group, string name, int width, int height, float scalex, float scaley, bool worldpanning)
		{
			// Initialize
			this.width = width;
			this.height = height;
			this.scale.x = scalex;
			this.scale.y = scaley;
			this.worldpanning = worldpanning; //mxd
			this.patches = new List<TexturePatch>();
			SetName(name);
			virtualname = "[" + group + "]/" + this.name; //mxd
			
			// We have no destructor
			GC.SuppressFinalize(this);
		}

		#endregion

		#region ================== Methods

		// This adds a patch to the texture
		public void AddPatch(TexturePatch patch)
		{
			// Add it
			patches.Add(patch);

			if(patch.LumpName == Name) hasPatchWithSameName = true; //mxd
		}
		
		// This loads the image
		protected override LocalLoadResult LocalLoadImage()
		{
			// Checks
			if(width == 0 || height == 0) return new LocalLoadResult(null);

			BitmapData bitmapdata = null;
			PixelColor* pixels = (PixelColor*)0;

            Bitmap bitmap = null;
            List<LogMessage> messages = new List<LogMessage>();

			// Create texture bitmap
			try
			{
				if(bitmap != null) bitmap.Dispose();
				bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
				bitmapdata = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
				pixels = (PixelColor*)bitmapdata.Scan0.ToPointer();
				General.ZeroMemory(new IntPtr(pixels), width * height * sizeof(PixelColor));
			}
			catch(Exception e)
			{
				// Unable to make bitmap
				messages.Add(new LogMessage(ErrorType.Error, "Unable to load texture image \"" + this.Name + "\". " + e.GetType().Name + ": " + e.Message));
			}

			int missingpatches = 0; //mxd

			if(!messages.Any(x => x.Type == ErrorType.Error))
			{
				// Go for all patches
				foreach(TexturePatch p in patches)
				{
					// Get the patch data stream
					string patchlocation = string.Empty; //mxd
					Stream patchdata = General.Map.Data.GetPatchData(p.LumpName, p.HasLongName, ref patchlocation);
					if(patchdata != null)
					{
						// Get a reader for the data
						IImageReader reader = ImageDataFormat.GetImageReader(patchdata, ImageDataFormat.DOOMPICTURE, General.Map.Data.Palette);
						if(reader is UnknownImageReader)
						{
							//mxd. Probably that's a flat?..
							if(General.Map.Config.MixTexturesFlats) 
							{
								reader = ImageDataFormat.GetImageReader(patchdata, ImageDataFormat.DOOMFLAT, General.Map.Data.Palette);
							}
							if(reader is UnknownImageReader) 
							{
                                // Data is in an unknown format!
                                messages.Add(new LogMessage(ErrorType.Error, "Patch lump \"" + Path.Combine(patchlocation, p.LumpName) + "\" data format could not be read, while loading texture \"" + this.Name + "\". Does this lump contain valid picture data at all?"));
								missingpatches++; //mxd
							}
						}

						if(!(reader is UnknownImageReader))
						{
                            // Draw the patch
                            patchdata.Seek(0, SeekOrigin.Begin);
							try { reader.DrawToPixelData(patchdata, pixels, width, height, p.X, p.Y); }
							catch(InvalidDataException)
							{
                                // Data cannot be read!
                                messages.Add(new LogMessage(ErrorType.Error, "Patch lump \"" + p.LumpName + "\" data format could not be read, while loading texture \"" + this.Name + "\". Does this lump contain valid picture data at all?"));
								missingpatches++; //mxd
							}
						}

                        // Done
                        patchdata.Dispose();
					}
					else
					{
                        // Missing a patch lump!
                        messages.Add(new LogMessage(ErrorType.Error, "Missing patch lump \"" + p.LumpName + "\" while loading texture \"" + this.Name + "\". Did you forget to include required resources?"));
						missingpatches++; //mxd
					}
				}

				// Done
				bitmap.UnlockBits(bitmapdata);
			}
				
			// Dispose bitmap if load failed
			if((bitmap != null) && (messages.Any(x => x.Type == ErrorType.Error) || missingpatches >= patches.Count)) //mxd. We can still display texture if at least one of the patches was loaded
			{
				bitmap.Dispose();
				bitmap = null;
			}

            return new LocalLoadResult(bitmap, messages);
		}

		#endregion
	}
}
