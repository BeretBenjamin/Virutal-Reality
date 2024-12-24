using System;
using System.Diagnostics.Tracing;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace rt;

public class RawCtMask : Geometry
{
    private readonly Vector _position;
    private readonly double _scale;
    private readonly ColorMap _colorMap;
    private readonly byte[] _data;

    private readonly int[] _resolution = new int[3];
    private readonly double[] _thickness = new double[3];
    private readonly Vector _v0;
    private readonly Vector _v1;

    public RawCtMask(string datFile, string rawFile, Vector position, double scale, ColorMap colorMap) : base(Color.NONE)
    {
        _position = position;
        _scale = scale;
        _colorMap = colorMap;

        var lines = File.ReadLines(datFile);
        foreach (var line in lines)
        {
            var kv = Regex.Replace(line, "[:\\t ]+", ":").Split(":");
            if (kv[0] == "Resolution")
            {
                _resolution[0] = Convert.ToInt32(kv[1]);
                _resolution[1] = Convert.ToInt32(kv[2]);
                _resolution[2] = Convert.ToInt32(kv[3]);
            }
            else if (kv[0] == "SliceThickness")
            {
                _thickness[0] = Convert.ToDouble(kv[1]);
                _thickness[1] = Convert.ToDouble(kv[2]);
                _thickness[2] = Convert.ToDouble(kv[3]);
            }
        }

        _v0 = position;
        _v1 = position + new Vector(_resolution[0] * _thickness[0] * scale, _resolution[1] * _thickness[1] * scale, _resolution[2] * _thickness[2] * scale);

        var len = _resolution[0] * _resolution[1] * _resolution[2];
        _data = new byte[len];
        using FileStream f = new FileStream(rawFile, FileMode.Open, FileAccess.Read);
        if (f.Read(_data, 0, len) != len)
        {
            throw new InvalidDataException($"Failed to read the {len}-byte raw data");
        }
    }

    private ushort Value(int x, int y, int z)
    {
        if (x < 0 || y < 0 || z < 0 || x >= _resolution[0] || y >= _resolution[1] || z >= _resolution[2])
        {
            return 0;
        }

        return _data[z * _resolution[1] * _resolution[0] + y * _resolution[0] + x];
    }

    public override Intersection GetIntersection(Line rayLine, double minDist, double maxDist)
    {
        double tMinX = (_v0.X - rayLine.X0.X) / rayLine.Dx.X;
        double tMaxX = (_v1.X - rayLine.X0.X) / rayLine.Dx.X;

        double tMinY = (_v0.Y - rayLine.X0.Y) / rayLine.Dx.Y;
        double tMaxY = (_v1.Y - rayLine.X0.Y) / rayLine.Dx.Y;
        
        double tMinZ = (_v0.Z - rayLine.X0.Z) / rayLine.Dx.Z;             
        double tMaxZ = (_v1.Z - rayLine.X0.Z) / rayLine.Dx.Z;

        double tMin = Math.Max(Math.Max(Math.Min(tMinX, tMaxX), Math.Min(tMinY, tMaxY)), Math.Min(tMinZ, tMaxZ));
        double tMax = Math.Min(Math.Min(Math.Max(tMinX, tMaxX), Math.Max(tMinY, tMaxY)), Math.Max(tMinZ, tMaxZ));

        if (tMax < Math.Max(tMin, 0.0)) return Intersection.NONE;

        double startIntersection = Math.Max(tMin, minDist);
        double endIntersection = Math.Min(tMax, maxDist);

        if (startIntersection > endIntersection) return Intersection.NONE;

        double stepSize = 0.2d;
        double firstIntersectionDistance = -1;
        Vector surfaceNormal = null;
        Color accumulatedColor = Color.NONE;
        double lastTransparency = 1.0; // lastTransparency := (1 - opacity)
        bool foundFirstIntersection = false;

        for (double t = startIntersection; t <= endIntersection; t += stepSize)
        {
            Vector pointOnRay = rayLine.CoordinateToPosition(t);
            int[] voxelIndex = GetIndexes(pointOnRay);
            ushort voxelValue = Value(voxelIndex[0], voxelIndex[1], voxelIndex[2]);

            if (voxelValue > 0)
            {
                if (!foundFirstIntersection)
                {
                    firstIntersectionDistance = t;
                    surfaceNormal = GetNormal(pointOnRay);
                    foundFirstIntersection = true;
                }

                Color voxelColor = GetColor(pointOnRay);
                double voxelOpacity = voxelColor.Alpha;

                // accumulatedColor += voxelColor * voxelOpacity * (1 - lastOpacity);
                accumulatedColor += voxelColor * voxelOpacity * lastTransparency;
                lastTransparency *= (1 - voxelOpacity);

                // accumulated transparency is <= 0, no need to continue
                if (lastTransparency <= 0)
                {
                    break;
                }
            }
        }

        if (!foundFirstIntersection) 
            return Intersection.NONE;

        return new Intersection(true, true, this, rayLine, firstIntersectionDistance, surfaceNormal, Material.FromColor(accumulatedColor), accumulatedColor);
    }



    private int[] GetIndexes(Vector v)
    {
        return new[]{
            (int)Math.Floor((v.X - _position.X) / _thickness[0] / _scale),
            (int)Math.Floor((v.Y - _position.Y) / _thickness[1] / _scale),
            (int)Math.Floor((v.Z - _position.Z) / _thickness[2] / _scale)};
    }
    private Color GetColor(Vector v)
    {
        int[] idx = GetIndexes(v);

        ushort value = Value(idx[0], idx[1], idx[2]);
        return _colorMap.GetColor(value);
    }

    private Vector GetNormal(Vector v)
    {
        int[] idx = GetIndexes(v);
        double x0 = Value(idx[0] - 1, idx[1], idx[2]);
        double x1 = Value(idx[0] + 1, idx[1], idx[2]);
        double y0 = Value(idx[0], idx[1] - 1, idx[2]);
        double y1 = Value(idx[0], idx[1] + 1, idx[2]);
        double z0 = Value(idx[0], idx[1], idx[2] - 1);
        double z1 = Value(idx[0], idx[1], idx[2] + 1);

        return new Vector(x1 - x0, y1 - y0, z1 - z0).Normalize();
    }
}