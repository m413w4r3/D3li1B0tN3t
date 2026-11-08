# P2N-C calibration

P2N-C maps D3B logical map points to captured-window pixels with a six-parameter affine transform. It fits only
`fit` anchors with OpenCV RANSAC and checks the result against independent `validation` anchors. A passing synthetic
fixture demonstrates deterministic math; it does not establish accuracy on a Unity screenshot.

## Coordinates and model

For each `LogicalCell`, the logical point is obtained only through `MapGeometry.PointOf(cell.Value)`. The mapper does
not copy Avalonia's `MapLayout` and does not convert between coordinate types implicitly. `Project(MapPoint)` keeps
double precision for residuals; `Map(MapPoint)` rounds to the nearest integer pixel, with midpoint values away from
zero.

```text
screenX = a * mapX + b * mapY + tx
screenY = c * mapX + d * mapY + ty
```

The returned OpenCV matrix must be a finite 2×3 `CV_64FC1` matrix. The linear component is rejected when the
absolute normalized determinant is at most `1e-12`; normalization makes the singularity check independent of the
pixel scale of the coefficients.

## Measured geometry and profile identity

`ClientAreaGeometry` stores the measured desktop origin, measured origin in the captured window, and measured width
and height. The validator does not assume that the client area starts at `(0, 0)`. It checks that the measured client
rectangle fits in the captured image and that desktop scale is finite and positive.

A profile records client build, map, XID/PID and other window identity fields, captured dimensions, measured client
geometry, scale, calibration version, mapper, anchors, validation residuals, creation time, and its synthetic label.
`CalibrationId` is a stable SHA-256 digest of those values and can bind a future authorization to this exact profile.
An unverified XID can still have a calibrated preview profile; the identity status is preserved. Action eligibility
remains the responsibility of the GUI action gate.

Profiles are invalidated when XID or PID, resolution, scale, client-area geometry, map, or calibration version
changes, when stored geometry contradicts the capture, or when the profile is older than 30 seconds. Window title is
not treated as identity evidence.

## Annotation JSON version 1

The loader accepts the mission's camel-case fields and requires positive dimensions, a supported version, recognized
roles, valid map cell identifiers, and measured points inside the image. The optional `synthetic` field defaults to
`false`. Loading assigns `ManualAnnotation` provenance regardless of the synthetic label; deterministic in-code test
fixtures use `SyntheticFixture` provenance. A synthetic document or request stays labelled synthetic in the result
and profile.

```json
{
  "formatVersion": 1,
  "clientBuild": "3.7.13.17-synthetic",
  "mapId": 191105026,
  "imageWidth": 800,
  "imageHeight": 600,
  "anchors": [
    { "cellId": 100, "pixelX": 420, "pixelY": 240, "role": "fit" },
    { "cellId": 120, "pixelX": 500, "pixelY": 300, "role": "validation" }
  ],
  "synthetic": true
}
```

The coordinates above only illustrate the format. Every loaded screen point is a manual annotation, never an
automatically verified observation.

## Fit checks and initial thresholds

At least eight fit anchors and three validation anchors are required. Validation cells must be unique and must not
also occur among fit cells. Fit anchors must be non-collinear in logical and measured coordinates. They must span at
least 20% of both image dimensions, cover at least 10% of image area, and occupy at least three quadrants. Quadrants
are measured against the full capture dimensions.

RANSAC uses a 3 px reprojection threshold, 5,000 iterations, 99% confidence, ten refinement iterations, and a fixed
OpenCV RNG seed. A lock around the seeded call keeps this validator's use deterministic. The initial acceptance
thresholds are:

| Metric | Threshold |
| --- | ---: |
| Median fit residual | ≤ 4 px |
| Maximum independent validation residual | ≤ 8 px |
| Any individual validation residual | ≤ 12 px |
| RANSAC inlier ratio among fit anchors | ≥ 80% |

All fit residuals include RANSAC rejects so outliers remain visible in metrics. Rejected indexes refer to the full
document anchor array. Validation anchors are never passed to the fit. Every measured and projected anchor must be
inside the captured image; residuals are computed from the double projection before integer rounding.

Metrics are returned for all attempts and include fit and validation counts, median and maximum errors, inlier ratio,
residual arrays, and rejected indexes. A profile is returned only when no calibration problem remains. Invalidated
profiles retain their metrics for diagnosis.

## Synthetic checks and limits

The deterministic exact fixture recovers `(a, b, tx, c, d, ty) = (15, 8, 70, 8, -3, 80)` with median fit and maximum
validation error reported as `0.000 px` at three decimal places and 12/12 fit inliers. The 12-fit-anchor outlier
fixture corrupts two annotations; RANSAC rejects both and retains 10/12 inliers (83.3%). Integer-pixel noise remains
within the initial 4 px median fit and 8 px maximum validation limits.

These fixtures test only the coordinate math and file contract. No real-client screenshot, capture path, automatic
vision, action gate, or executor is part of P2N-C. Initial thresholds are design values and require later measurement
against supervised, manually annotated client screenshots.
