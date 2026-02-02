// Author:
//       Jonathan Pobst <monkey@jpobst.com>
//
// Copyright (c) 2010 Jonathan Pobst
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

// Some functions are from Paint.NET:

/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) dotPDN LLC, Rick Brewster, Tom Jackson, and contributors.     //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See license-pdn.txt for full licensing and attribution details.             //
/////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cairo;

namespace Pinta.Core;

partial class CairoExtensions
{
	public static Pattern ToTiledPattern (this Surface surface)
		=> new SurfacePattern (surface) { Extend = Extend.Repeat };

	public static Pattern CreateTransparentBackgroundPattern (int size)
		=>
			CreateTransparentBackgroundSurface (size)
			.ToTiledPattern ();

	/// <summary>
	/// Sets the dash pattern from a string
	/// (see <see cref="CreateDashPattern"/>).
	/// </summary>
	/// <returns>Returns false if dash pattern invalid or would draw a normal line, returns true if draws a dash pattern.</returns>
	public static bool SetDashFromString (
		this Context context,
		string dash_pattern,
		double brush_width,
		LineCap line_cap = LineCap.Butt)
	{
		bool isValidDashPattern = CreateDashPattern (
			dash_pattern,
			brush_width,
			line_cap,
			out var dashes,
			out var offset);

		context.SetDash (
			dashes,
			offset);

		return isValidDashPattern;
	}

	/// <summary>
	/// Given a string pattern consisting of dashes and spaces, creates the Cairo dash pattern.
	/// Any other characters are treated as a space.
	/// See https://www.cairographics.org/manual/cairo-cairo-t.html#cairo-set-dash
	/// </summary>
	/// <param name="dash_pattern">The dash pattern string.</param>
	/// <param name="brush_width">The width of the brush.</param>
	/// <param name="line_cap">The line cap style being used.</param>
	/// <param name="dash_list">The Cairo dash pattern.</param>
	/// <param name="offset">The offset into the dash pattern to begin drawing from.</param>
	/// <returns>Returns false if dash pattern invalid or would draw a normal line, returns true if draws a dash pattern. See <see cref="IsValidDashPattern"/></returns>
	public static bool CreateDashPattern (
		string dash_pattern,
		double brush_width,
		LineCap line_cap,
		out double[] dash_list,
		out double offset)
	{
		// An empty cairo pattern, a pattern with no dashes, or a pattern with a single dash just draws a normal line.
		// Cairo draws a normal line when the dash list is empty.
		if (!IsValidDashPattern (dash_pattern)) {
			dash_list = [];
			offset = 0.0;
			return false;
		}

		List<double> dashes = [];

		// Count the number of consecutive dashes / spaces.
		// e.g. "---  - " produces { 3.0, 2.0, 1.0, 1.0 }
		{
			var is_dash = dash_pattern.Select (c => c == '-').ToArray ();
			int count = 0;

			for (int i = 0; i < dash_pattern.Length; ++i, ++count) {

				if (i <= 0 || is_dash[i] == is_dash[i - 1])
					continue;

				dashes.Add (count);
				count = 0;
			}

			dashes.Add (count);
		}

		// The cairo pattern must have an even number of dash and space sequences to loop,
		// so add a zero length space if the string pattern ended with a dash.
		if (dash_pattern.EndsWith ('-'))
			dashes.Add (0.0);

		// The cairo pattern starts with a dash, so if the string pattern
		// started with a space we need to add a zero-width dash.
		// However, we can't draw a zero-width dash if the line cap is square, so instead
		// we need to shift the space to the end of the pattern and then use the offset parameter
		// to start drawing from there.
		double? offset_from_end = null;
		if (!dash_pattern.StartsWith ('-')) {
			offset_from_end = dashes[0];
			dashes.RemoveAt (0);

			// From above, the dash pattern must already have a space at the end so
			// we can just increase its size.
			// The list is non-empty since patterns containing only a space result in an early exit.
			dashes[^1] += offset_from_end.Value;
		}

		// Each dash / space is the size of the brush width.
		// Line caps add some complexity - dashes extend visually by 0.5 * brush_width on
		// either side (e.g. a dash of size 0 still draws a square), so we need to
		// adjust the sizes accordingly for this padding.
		// Cairo seems to sometimes ignore zero sized dashes though (e.g. for a dash pattern
		// such as "- -" => { 0, 30, 0, 15 }, with brush width 15), so we always draw at least a length of 1.

		double dash_size_offset =
			line_cap == LineCap.Butt
			? 0.0
			: -brush_width;

		double space_size_offset =
			line_cap == LineCap.Butt
			? 0.0
			: brush_width;

		dash_list =
			dashes
			.Select ((dash, i) => {
				if ((i % 2) == 0)
					return Math.Max (dash * brush_width + dash_size_offset, 1.0);
				else
					return dash * brush_width + space_size_offset;
			})
			.ToArray ();

		offset =
			offset_from_end.HasValue
			? dash_list.Sum () - (offset_from_end.Value * brush_width + 0.5 * space_size_offset)
			: 0.0;

		return true;
	}

	/// <summary>
	/// Returns the validity of the dash pattern.
	/// </summary>
	/// <param name="dash_pattern">The dash pattern string.</param>
	/// <returns>Returns false if dash pattern invalid or would draw a normal line, returns true if draws a dash pattern.</returns>
	public static bool IsValidDashPattern (string dash_pattern)
	{
		// dashpattern "-" and "" produce different results at high brush size (see #733), so we default "-" to "" (a normal line.)
		return dash_pattern.Contains ('-') && dash_pattern != "-";
	}

	public static void FillStencilByColor(
	ImageSurface surface,
	BitMask stencil,
	ColorBgra cmp,
	int tolerance,
	out RectangleD boundingBox,
	Region limitRegion,
	bool limitToSelection)
{
	int width = surface.Width;
	int height = surface.Height;

	stencil.Clear(false);

	RectangleI[] scans;

	if (limitToSelection) {
		var excluded = CreateRegion(new RectangleI(0, 0, stencil.Width, stencil.Height));
		excluded.Xor(limitRegion);

		int n = excluded.GetNumRectangles();
		scans = new RectangleI[n];

		for (int i = 0; i < n; ++i) {
			excluded.GetRectangle(i, out var r);
			scans[i] = r.ToRectangleI();
		}

		for (int i = 0; i < n; ++i)
			stencil.Set(scans[i], true);
	} else {
		scans = Array.Empty<RectangleI>();
	}

	int globalLeft = int.MaxValue;
	int globalRight = int.MinValue;
	int globalTop = int.MaxValue;
	int globalBottom = int.MinValue;

	object bboxLock = new();

	Parallel.For(
		0,
		height,
		() => (
			left: int.MaxValue,
			right: int.MinValue,
			top: int.MaxValue,
			bottom: int.MinValue
		),
		(y, _, local) => {

			// Obtain span locally (legal)
			ReadOnlySpan<ColorBgra> data = surface.GetReadOnlyPixelData();
			ReadOnlySpan<ColorBgra> row = data.Slice(y * width, width);

			bool found = false;

			for (int x = 0; x < width; ++x) {
				if (!ColorBgra.ColorsWithinTolerance(cmp, row[x], tolerance))
					continue;

				stencil.Set(x, y, true);

				if (x < local.left) local.left = x;
				if (x > local.right) local.right = x;

				found = true;
			}

			if (found) {
				if (y < local.top) local.top = y;
				if (y > local.bottom) local.bottom = y;
			}

			return local;
		},
		local => {
			if (local.left == int.MaxValue)
				return;

			lock (bboxLock) {
				if (local.left < globalLeft) globalLeft = local.left;
				if (local.right > globalRight) globalRight = local.right;
				if (local.top < globalTop) globalTop = local.top;
				if (local.bottom > globalBottom) globalBottom = local.bottom;
			}
		}
	);

	for (int i = 0; i < scans.Length; ++i)
		stencil.Set(scans[i], false);

	if (globalLeft == int.MaxValue) {
		boundingBox = RectangleD.Zero;
		return;
	}

	boundingBox = new RectangleD(
		globalLeft,
		globalTop,
		globalRight - globalLeft + 1,
		globalBottom - globalTop + 1);
}

	public static void FillStencilFromPoint(
	ImageSurface surface,
	BitMask stencil,
	PointI start,
	int tolerance,
	out RectangleD boundingBox,
	Region limitRegion,
	bool limitToSelection)
{
	ReadOnlySpan<ColorBgra> data = surface.GetReadOnlyPixelData();
	int width = surface.Width;
	int height = surface.Height;

	ColorBgra cmp = surface.GetColorBgra(data, width, start);

	int left = start.X;
	int right = start.X;
	int top = start.Y;
	int bottom = start.Y;

	stencil.Clear(false);

	RectangleI[] scans;

	if (limitToSelection) {
		var excluded = CreateRegion(new RectangleI(0, 0, stencil.Width, stencil.Height));
		excluded.Xor(limitRegion);

		int n = excluded.GetNumRectangles();
		scans = new RectangleI[n];

		for (int i = 0; i < n; ++i) {
			excluded.GetRectangle(i, out var r);
			scans[i] = r.ToRectangleI();
		}

		for (int i = 0; i < n; ++i)
			stencil.Set(scans[i], true);
	} else {
		scans = Array.Empty<RectangleI>();
	}

	// Simple array-backed queue
	PointI[] queue = new PointI[1024];
	int qHead = 0;
	int qTail = 0;

	queue[qTail++] = start;

	while (qHead != qTail) {
		PointI pt = queue[qHead++];
		if (qHead == queue.Length) qHead = 0;

		int y = pt.Y;
		int rowOffset = y * width;
		ReadOnlySpan<ColorBgra> row = data.Slice(rowOffset, width);

		int xLeft = pt.X;
		int xRight = pt.X;

		while (xLeft - 1 >= 0 &&
		       !stencil.Get(xLeft - 1, y) &&
		       ColorBgra.ColorsWithinTolerance(cmp, row[xLeft - 1], tolerance)) {
			--xLeft;
			stencil.Set(xLeft, y, true);
		}

		while (xRight < width &&
		       !stencil.Get(xRight, y) &&
		       ColorBgra.ColorsWithinTolerance(cmp, row[xRight], tolerance)) {
			stencil.Set(xRight, y, true);
			++xRight;
		}

		--xRight;

		if (xLeft < left) left = xLeft;
		if (xRight > right) right = xRight;
		if (y < top) top = y;
		if (y > bottom) bottom = y;

		// Check row above
		if (y > 0) {
			int oy = y - 1;
			int offset = oy * width;
			ReadOnlySpan<ColorBgra> orow = data.Slice(offset, width);

			int sx = xLeft;
			while (sx <= xRight) {
				while (sx <= xRight &&
				       (stencil.Get(sx, oy) ||
				        !ColorBgra.ColorsWithinTolerance(cmp, orow[sx], tolerance))) {
					++sx;
				}

				if (sx <= xRight) {
					queue[qTail++] = new PointI(sx, oy);
					if (qTail == queue.Length) qTail = 0;

					while (sx <= xRight &&
					       !stencil.Get(sx, oy) &&
					       ColorBgra.ColorsWithinTolerance(cmp, orow[sx], tolerance)) {
						++sx;
					}
				}
			}
		}

		// Check row below
		if (y < height - 1) {
			int oy = y + 1;
			int offset = oy * width;
			ReadOnlySpan<ColorBgra> orow = data.Slice(offset, width);

			int sx = xLeft;
			while (sx <= xRight) {
				while (sx <= xRight &&
				       (stencil.Get(sx, oy) ||
				        !ColorBgra.ColorsWithinTolerance(cmp, orow[sx], tolerance))) {
					++sx;
				}

				if (sx <= xRight) {
					queue[qTail++] = new PointI(sx, oy);
					if (qTail == queue.Length) qTail = 0;

					while (sx <= xRight &&
					       !stencil.Get(sx, oy) &&
					       ColorBgra.ColorsWithinTolerance(cmp, orow[sx], tolerance)) {
						++sx;
					}
				}
			}
		}
	}

	for (int i = 0; i < scans.Length; ++i)
		stencil.Set(scans[i], false);

	boundingBox = new RectangleD(
		left,
		top,
		right - left + 1,
		bottom - top + 1);
}
}
