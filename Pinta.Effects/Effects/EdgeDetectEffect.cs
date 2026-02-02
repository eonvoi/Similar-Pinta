/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) dotPDN LLC, Rick Brewster, Tom Jackson, and contributors.     //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See license-pdn.txt for full licensing and attribution details.             //
//                                                                             //
// Ported to Pinta by: Krzysztof Marecki <marecki.krzysztof@gmail.com>         //
/////////////////////////////////////////////////////////////////////////////////

using System;
using System.Threading.Tasks;
using Cairo;
using Pinta.Core;

namespace Pinta.Effects;

public sealed class EdgeDetectEffect : BaseEffect
{
	public override string Icon
		=> Resources.Icons.EffectsStylizeEdgeDetect;

	public sealed override bool IsTileable
		=> true;

	public override string Name
		=> Translations.GetString ("Edge Detect");

	public override bool IsConfigurable
		=> true;

	public override string EffectMenuCategory
		=> Translations.GetString ("Stylize");

	public EdgeDetectData Data => (EdgeDetectData) EffectData!;  // NRT - Set in constructor

	private readonly IChromeService chrome;
	private readonly IWorkspaceService workspace;
	public EdgeDetectEffect (IServiceProvider services)
	{
		chrome = services.GetService<IChromeService> ();
		workspace = services.GetService<IWorkspaceService> ();
		EffectData = new EdgeDetectData ();
	}

	public override Task<bool> LaunchConfiguration ()
		=> chrome.LaunchSimpleEffectDialog (this, workspace);

	public override void Render (ImageSurface src, ImageSurface dest, ReadOnlySpan<RectangleI> rois)
	{
		var weights = ComputeWeights (Data.Angle.ToRadians ());
		ColorDifference.RenderColorDifferenceEffect (weights, src, dest, rois);
	}

private static double[] ComputeWeights(RadiansAngle angle)
{
    const double AngleDelta = Math.PI / 4.0;

    double a = angle.Radians;

    // row-major order:
    // [0] [1] [2]
    // [3] [4] [5]
    // [6] [7] [8]
    var weights = new double[9];

    weights[0] = Math.Cos(a + AngleDelta);
    weights[1] = Math.Cos(a + 2.0 * AngleDelta);
    weights[2] = Math.Cos(a + 3.0 * AngleDelta);

    weights[3] = Math.Cos(a);
    weights[4] = 0.0;
    weights[5] = Math.Cos(a + 4.0 * AngleDelta);

    weights[6] = Math.Cos(a - AngleDelta);
    weights[7] = Math.Cos(a - 2.0 * AngleDelta);
    weights[8] = Math.Cos(a - 3.0 * AngleDelta);

    return weights;
}
}

public sealed class EdgeDetectData : EffectData
{
	[Caption ("Angle")]
	public DegreesAngle Angle { get; set; } = new (45);
}
