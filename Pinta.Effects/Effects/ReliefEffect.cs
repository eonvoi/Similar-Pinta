/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) dotPDN LLC, Rick Brewster, Tom Jackson, and contributors.     //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See license-pdn.txt for full licensing and attribution details.             //
//                                                                             //
// Ported to Pinta by: Marco Rolappe <m_rolappe@gmx.net>                       //
/////////////////////////////////////////////////////////////////////////////////

using System;
using System.Threading.Tasks;
using Pinta.Core;

namespace Pinta.Effects;

public sealed class ReliefEffect : BaseEffect
{
	private readonly IChromeService chrome;
	private readonly IWorkspaceService workspace;
	public ReliefEffect (IServiceProvider services)
	{
		chrome = services.GetService<IChromeService> ();
		workspace = services.GetService<IWorkspaceService> ();
		EffectData = new ReliefData ();
	}

	public ReliefData Data => (ReliefData) EffectData!;

	public override bool IsConfigurable => true;

	public sealed override bool IsTileable => true;

	public override string EffectMenuCategory => Translations.GetString ("Stylize");

	public override Task<bool> LaunchConfiguration ()
		=> chrome.LaunchSimpleEffectDialog (this, workspace);

	public override string Icon => Resources.Icons.EffectsStylizeRelief;

	public override string Name => Translations.GetString ("Relief");

	// Algorithm Code Ported From PDN
	public override void Render (Cairo.ImageSurface source, Cairo.ImageSurface destination, ReadOnlySpan<RectangleI> rois)
	{
		var weights = ComputeWeights (Data.Angle.ToRadians ());
		ColorDifference.RenderColorDifferenceEffect (weights, source, destination, rois);
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


public sealed class ReliefData : EffectData
{
	[Caption ("Angle")]
	public DegreesAngle Angle { get; set; } = new (45);
}
