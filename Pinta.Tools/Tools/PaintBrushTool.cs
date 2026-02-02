// 
// PaintBrushTool.cs
//  
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


using System;
using System.Linq;
using Cairo;
using Gtk;
using Pinta.Core;

namespace Pinta.Tools;

public sealed class PaintBrushTool : BaseBrushTool
{
	private readonly IPaintBrushService brushes;

	private BasePaintBrush? default_brush;
	private BasePaintBrush? active_brush;
	private PointI? last_point = PointI.Zero;
	private uint? open_repeating_draw_id;
	private Box brush_specific_options_box;

	public PaintBrushTool (IServiceProvider services) : base (services)
	{
		brushes = services.GetService<IPaintBrushService> ();

		default_brush = brushes.FirstOrDefault ();
		active_brush = default_brush;

		brushes.BrushAdded += (_, _) => RebuildBrushComboBox ();
		brushes.BrushRemoved += (_, _) => RebuildBrushComboBox ();

		brush_specific_options_box = Box.New (Orientation.Horizontal, 10);
	}

	public override string Name => Translations.GetString ("Paintbrush");
	public override string Icon => Pinta.Resources.Icons.ToolPaintBrush;
	public override string StatusBarText => Translations.GetString ("Left click to draw with primary color, right click to draw with secondary color.");
	public override bool CursorChangesOnZoom => true;
	public override Gdk.Key ShortcutKey => new (Gdk.Constants.KEY_B);
	public override int Priority => 21;

	public override Gdk.Cursor DefaultCursor {
		get {
			var icon = GdkExtensions.CreateIconWithShape ("Cursor.Paintbrush.png",
							CursorShape.Ellipse, BrushWidth, 8, 24,
							out var iconOffsetX, out var iconOffsetY);

			return Gdk.Cursor.NewFromTexture (icon, iconOffsetX, iconOffsetY, null);
		}
	}

	protected override void OnBuildToolBar (Box tb)
	{
		base.OnBuildToolBar (tb);

		tb.Append (Separator);

		tb.Append (BrushLabel);
		tb.Append (BrushComboBox);

		RebuildBrushSpecificOptions ();
		brush_specific_options_box.MarginStart = 10;
		tb.Append (brush_specific_options_box);
	}

	protected override void OnMouseDown (Document document, ToolMouseEventArgs e)
	{
		// Clear tool layer at start of stroke
		document.Layers.ToolLayer.Clear ();
		document.Layers.ToolLayer.Hidden = false;

		base.OnMouseDown (document, e);

		active_brush?.DoMouseDown ();
		last_point = e.Point; // start tracking
	}

	protected override void OnMouseMove (Document document, ToolMouseEventArgs e)
	{
		if (active_brush is null || mouse_button is not (MouseButton.Left or MouseButton.Right)) {
			last_point = null;
			return;
		}

		var strokeColor = mouse_button switch {
			MouseButton.Right => new Color (
			    Palette.SecondaryColor.R,
			    Palette.SecondaryColor.G,
			    Palette.SecondaryColor.B,
			    Palette.SecondaryColor.A * active_brush.StrokeAlphaMultiplier
			),
			_ => new Color (
			    Palette.PrimaryColor.R,
			    Palette.PrimaryColor.G,
			    Palette.PrimaryColor.B,
			    Palette.PrimaryColor.A * active_brush.StrokeAlphaMultiplier
			)
		};

		if (!last_point.HasValue)
			last_point = e.Point;

		if (document.Workspace.PointInCanvas (e.PointDouble))
			surface_modified = true;

		// Draw preview into ToolLayer.Surface
		var toolSurf = document.Layers.ToolLayer.Surface;
		using var g = document.CreateClippedToolContext ();
		g.Antialias = UseAntialiasing ? Antialias.Subpixel : Antialias.None;
		g.LineWidth = BrushWidth;
		g.LineJoin = LineJoin.Round;
		g.LineCap = LineCap.Round;
		g.SetSourceColor (strokeColor);

		var strokeArgs = new BrushStrokeArgs (strokeColor, e.Point, last_point.Value);
		CancelRepeatingDraw ();

		// Draw current segment of stroke into ToolLayer
		var dirtyRect = active_brush.DoMouseMove (g, toolSurf, strokeArgs);

		// Invalidate the proper region
		if (document.Workspace.IsPartiallyOffscreen (dirtyRect))
			document.Workspace.Invalidate ();
		else
			document.Workspace.Invalidate (document.ClampToImageSize (dirtyRect));

		// Support repeating brushes
		if (active_brush.MillisecondsBeforeReapply != 0)
			open_repeating_draw_id = GLib.Functions.TimeoutAdd (
			    GLib.Constants.PRIORITY_DEFAULT,
			    active_brush.MillisecondsBeforeReapply,
			    () => { OnMouseMove (document, e); return true; }
			);

		last_point = e.Point;
	}

	protected override void OnMouseUp (Document document, ToolMouseEventArgs e)
	{
		CancelRepeatingDraw ();
		active_brush?.DoMouseUp ();

		var toolLayer = document.Layers.ToolLayer;
		var userLayer = document.Layers.CurrentUserLayer;

		// Instead of creating a new Context, use the ToolLayer surface directly
		// with a temporary context to merge into the user layer
		using (var g = new Context (userLayer.Surface)) {
			// Explicitly set operator to SourceOver for normal blending
			g.Operator = Operator.Over;

			// Reset transformation if tool layer has any transform
			g.IdentityMatrix ();

			// Paint tool layer pixels onto user layer
			g.SetSourceSurface (toolLayer.Surface, 0, 0);
			g.Paint ();
		}

		// Now safe to clear tool layer
		toolLayer.Clear ();
		toolLayer.Hidden = true;

		last_point = null;

		base.OnMouseUp (document, e);

		// Force a canvas redraw immediately to display committed pixels
		document.Workspace.Invalidate ();
	}


	protected override void OnSaveSettings (ISettingsService settings)
	{
		base.OnSaveSettings (settings);

		if (brush_combo_box is not null)
			settings.PutSetting (SettingNames.PAINT_BRUSH_BRUSH, brush_combo_box.ComboBox.Active);

		if (active_brush is not null) {
			foreach (var option in active_brush.Options) {
				option.SaveValueToSettings (settings);
			}
		}
	}

	private Label? brush_label;
	private ToolBarComboBox? brush_combo_box;
	private Gtk.Separator? separator;

	private Gtk.Separator Separator => separator ??= GtkExtensions.CreateToolBarSeparator ();
	private Label BrushLabel => brush_label ??= Label.New (string.Format (" {0}:  ", Translations.GetString ("Type")));

	private ToolBarComboBox BrushComboBox {
		get {
			if (brush_combo_box is null) {
				brush_combo_box = new ToolBarComboBox (100, 0, false);
				brush_combo_box.ComboBox.OnChanged += (o, e) => {
					var brush_name = brush_combo_box.ComboBox.GetActiveText ();
					active_brush = brushes.SingleOrDefault (brush => brush.Name == brush_name) ?? default_brush;
				};

				RebuildBrushComboBox ();

				var brush = Settings.GetSetting (SettingNames.PAINT_BRUSH_BRUSH, 0);

				if (brush < brush_combo_box.ComboBox.Model.IterNChildren (null))
					brush_combo_box.ComboBox.Active = brush;
			}

			return brush_combo_box;
		}
	}

	/// <summary>
	/// Rebuild the list of brushes.
	/// </summary>
	private void RebuildBrushComboBox ()
	{
		default_brush = brushes.FirstOrDefault ();

		BrushComboBox.ComboBox.RemoveAll ();

		foreach (var brush in brushes)
			BrushComboBox.ComboBox.AppendText (brush.Name);

		BrushComboBox.ComboBox.Active = 0;
		BrushComboBox.ComboBox.OnChanged += (cbx, ev) => RebuildBrushSpecificOptions ();
	}

	private void CancelRepeatingDraw ()
	{
		if (open_repeating_draw_id != null) {
			GLib.Functions.SourceRemove (open_repeating_draw_id.Value);
			open_repeating_draw_id = null;
		}
	}

	private void RebuildBrushSpecificOptions ()
	{
		brush_specific_options_box.RemoveAll ();
		if (active_brush is not null) {
			foreach (var option in active_brush.Options) {
				brush_specific_options_box.Append (ToolOptionWidgetService.GetWidgetForOption (option));
			}
		}
	}
}
