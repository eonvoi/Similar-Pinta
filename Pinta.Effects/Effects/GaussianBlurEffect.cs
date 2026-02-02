/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) dotPDN LLC, Rick Brewster, Tom Jackson, and contributors.     //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See license-pdn.txt for full licensing and attribution details.             //
//                                                                             //
// Ported to Pinta by: Jonathan Pobst <monkey@jpobst.com>                      //
/////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Cairo;
using Pinta.Core;

namespace Pinta.Effects;

public sealed class GaussianBlurEffect : BaseEffect
{
	public override string Icon => Resources.Icons.EffectsBlursGaussianBlur;

	public sealed override bool IsTileable => true;

	public override string Name => Translations.GetString ("Gaussian Blur");

	public override bool IsConfigurable => true;

	public override string EffectMenuCategory => Translations.GetString ("Blurs");

	public GaussianBlurData Data => (GaussianBlurData) EffectData!;  // NRT - Set in constructor

	private readonly IChromeService chrome;
	private readonly IWorkspaceService workspace;
	public GaussianBlurEffect (IServiceProvider services)
	{
		chrome = services.GetService<IChromeService> ();
		workspace = services.GetService<IWorkspaceService> ();
		EffectData = new GaussianBlurData ();
	}

	public override Task<bool> LaunchConfiguration ()
		=> chrome.LaunchSimpleEffectDialog (this, workspace);

	#region Algorithm Code Ported From PDN

	public static ImmutableArray<int> CreateGaussianBlurRow (int amount)
	{
		int size = 1 + (amount * 2);
		var weights = ImmutableArray.CreateBuilder<int> (size);
		weights.Count = size;

		for (int i = 0; i <= amount; ++i) {
			// 1 + aa - aa + 2ai - ii
			weights[i] = 16 * (i + 1);
			weights[size - i - 1] = weights[i];
		}

		return weights.MoveToImmutable ();
	}

	public override void Render (ImageSurface src, ImageSurface dest, ReadOnlySpan<RectangleI> rois)
	{
		if (Data.Radius == 0)
			return; // Copy src to dest

		int r = Data.Radius;
		ImmutableArray<int> w = CreateGaussianBlurRow (r);
		int wlen = w.Length;

		Span<long> waSums = stackalloc long[wlen];
		Span<long> wcSums = stackalloc long[wlen];
		Span<long> aSums = stackalloc long[wlen];
		Span<long> bSums = stackalloc long[wlen];
		Span<long> gSums = stackalloc long[wlen];
		Span<long> rSums = stackalloc long[wlen];

		int src_width = src.Width;
		int src_height = src.Height;

		ReadOnlySpan<ColorBgra> src_data = src.GetReadOnlyPixelData ();
		Span<ColorBgra> dst_data = dest.GetPixelData ();

		foreach (var rect in rois) {

			if (rect.Height < 1 || rect.Width < 1)
				continue;

			for (int y = rect.Top; y <= rect.Bottom; ++y) {

				long waSum = 0;
				long wcSum = 0;
				long aSum = 0;
				long bSum = 0;
				long gSum = 0;
				long rSum = 0;

				var dst_row = dst_data.Slice (y * src_width, src_width);

				for (int wx = 0; wx < wlen; ++wx) {

					int srcX = rect.Left + wx - r;
					unchecked {
						waSums[wx] = 0;
						wcSums[wx] = 0;
						aSums[wx] = 0;
						bSums[wx] = 0;
						gSums[wx] = 0;
						rSums[wx] = 0;
					}
					if ((uint) srcX >= (uint) src_width)
						continue;

					for (int wy = 0; wy < wlen; ++wy) {

						int srcY = y + wy - r;
						if ((uint) srcY >= (uint) src_height)
							continue;

						int srcIndex = srcY * src_width + srcX;
						ColorBgra c = src_data[srcIndex].ToStraightAlpha ();

						int wp = w[wy];

						waSums[wx] += wp;
						wp *= c.A + (c.A >> 7);
						wcSums[wx] += wp;
						wp >>= 8;

						if (c.A > 0) {
							unchecked {
								aSums[wx] += wp * c.A;
								bSums[wx] += wp * c.B;
								gSums[wx] += wp * c.G;
								rSums[wx] += wp * c.R;
							}
						}
					}
					unchecked {
						int wwx = w[wx];
						waSum += wwx * waSums[wx];
						wcSum += wwx * wcSums[wx];
						aSum += wwx * aSums[wx];
						bSum += wwx * bSums[wx];
						gSum += wwx * gSums[wx];
						rSum += wwx * rSums[wx];
					}
				}

				wcSum >>= 8;

				if (waSum == 0 || wcSum == 0) {
					dst_row[rect.Left] = ColorBgra.Zero;
				} else {
					byte alpha = (byte) (aSum / waSum);
					byte blue = (byte) (bSum / wcSum);
					byte green = (byte) (gSum / wcSum);
					byte red = (byte) (rSum / wcSum);

					dst_row[rect.Left] =
						ColorBgra.FromBgra (blue, green, red, alpha).ToPremultipliedAlpha ();
				}

				for (int x = rect.Left + 1; x <= rect.Right; ++x) {

					waSums.Slice (1).CopyTo (waSums);
					wcSums.Slice (1).CopyTo (wcSums);
					aSums.Slice (1).CopyTo (aSums);
					bSums.Slice (1).CopyTo (bSums);
					gSums.Slice (1).CopyTo (gSums);
					rSums.Slice (1).CopyTo (rSums);

					waSum = 0;
					wcSum = 0;
					aSum = 0;
					bSum = 0;
					gSum = 0;
					rSum = 0;

					for (int wx = 0; wx < wlen - 1; ++wx) {
						long wwx = w[wx];
						waSum += wwx * waSums[wx];
						wcSum += wwx * wcSums[wx];
						aSum += wwx * aSums[wx];
						bSum += wwx * bSums[wx];
						gSum += wwx * gSums[wx];
						rSum += wwx * rSums[wx];
					}

					int last = wlen - 1;

					waSums[last] = 0;
					wcSums[last] = 0;
					aSums[last] = 0;
					bSums[last] = 0;
					gSums[last] = 0;
					rSums[last] = 0;

					int srcX = x + last - r;

					if ((uint) srcX < (uint) src_width) {

						for (int wy = 0; wy < wlen; ++wy) {

							int srcY = y + wy - r;
							if ((uint) srcY >= (uint) src_height)
								continue;

							int srcIndex = srcY * src_width + srcX;
							ColorBgra c = src_data[srcIndex].ToStraightAlpha ();

							int wp = w[wy];

							waSums[last] += wp;
							wp *= c.A + (c.A >> 7);
							wcSums[last] += wp;
							wp >>= 8;

							if (c.A > 0) {
								aSums[last] += wp * c.A;
								bSums[last] += wp * c.B;
								gSums[last] += wp * c.G;
								rSums[last] += wp * c.R;
							}
						}

						int wr = w[last];
						waSum += wr * waSums[last];
						wcSum += wr * wcSums[last];
						aSum += wr * aSums[last];
						bSum += wr * bSums[last];
						gSum += wr * gSums[last];
						rSum += wr * rSums[last];
					}

					wcSum >>= 8;

					if (waSum == 0 || wcSum == 0) {
						dst_row[x] = ColorBgra.Zero;
					} else {
						byte alpha = (byte) (aSum / waSum);
						byte blue = (byte) (bSum / wcSum);
						byte green = (byte) (gSum / wcSum);
						byte red = (byte) (rSum / wcSum);

						dst_row[x] =
							ColorBgra.FromBgra (blue, green, red, alpha).ToPremultipliedAlpha ();
					}
				}
			}
		}
	}
	#endregion

	public sealed class GaussianBlurData : EffectData
	{
		[Caption ("Radius")]
		[MinimumValue (0), MaximumValue (200)]
		public int Radius { get; set; } = 2;

		[Skip]
		public override bool IsDefault => Radius == 0;
	}
}
