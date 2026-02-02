/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) Rick Brewster, Tom Jackson, and past contributors.            //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See license-pdn.txt for full licensing and attribution details.             //
/////////////////////////////////////////////////////////////////////////////////

using System;
using Cairo;

namespace Pinta.Core;

/// <summary>
/// ColorDifferenctEffect is a utility class for difference effects
/// that have floating point (double) convolution filters.
/// its architecture is just like ConvolutionFilterEffect, adding a
/// function (RenderColorDifferenceEffect) called from Render.
/// It is also limited to 3x3 kernels.
/// (Chris Crosetto)
/// </summary>
public static class ColorDifference
{
    // weights must be length 9, row-major: [0..2]=row0, [3..5]=row1, [6..8]=row2
    public static void RenderColorDifferenceEffect(
        ReadOnlySpan<double> weights,
        ImageSurface source,
        ImageSurface destination,
        ReadOnlySpan<RectangleI> rois)
    {
        if (weights.Length != 9)
            throw new ArgumentException("Must contain exactly 9 elements", nameof(weights));

        RectangleI bounds = source.GetBounds();
        int width = bounds.Width;
        int height = bounds.Height;

        ReadOnlySpan<ColorBgra> src = source.GetReadOnlyPixelData();
        Span<ColorBgra> dst = destination.GetPixelData();

        foreach (var rect in rois)
        {
            foreach (var pixel in Tiling.GeneratePixelOffsets(rect, source.GetSize()))
            {
                int x = pixel.coordinates.X;
                int y = pixel.coordinates.Y;

                // clamp kernel area to image bounds
                int x0 = x > bounds.X ? -1 : 0;
                int x1 = x < bounds.X + width - 1 ? 1 : 0;
                int y0 = y > bounds.Y ? -1 : 0;
                int y1 = y < bounds.Y + height - 1 ? 1 : 0;

                double rSum = 0;
                double gSum = 0;
                double bSum = 0;

                int baseIndex = y * width + x;

                for (int ky = y0; ky <= y1; ky++)
                {
                    int srcRow = baseIndex + ky * width;
                    int wRow = (ky + 1) * 3;

                    for (int kx = x0; kx <= x1; kx++)
                    {
                        double w = weights[wRow + (kx + 1)];
                        ColorBgra c = src[srcRow + kx];

                        rSum += w * c.R;
                        gSum += w * c.G;
                        bSum += w * c.B;
                    }
                }

                dst[pixel.memoryOffset] = ColorBgra.FromBgra(
                    Utility.ClampToByte(bSum),
                    Utility.ClampToByte(gSum),
                    Utility.ClampToByte(rSum),
                    255);
            }
        }
    }
}