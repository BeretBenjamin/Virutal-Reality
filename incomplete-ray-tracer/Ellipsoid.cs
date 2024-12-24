using System;


namespace rt
{
    public class Ellipsoid : Geometry
    {
        private Vector Center { get; }
        private Vector SemiAxesLength { get; }
        private double Radius { get; }

        public Ellipsoid(Vector center, Vector semiAxesLength, double radius, Material material, Color color) : base(material, color)
        {
            Center = center;
            SemiAxesLength = semiAxesLength;
            Radius = radius;
        }

        public Ellipsoid(Vector center, Vector semiAxesLength, double radius, Color color) : base(color)
        {
            Center = center;
            SemiAxesLength = semiAxesLength;
            Radius = radius;
        }

        private Vector ComputeNormal(Vector center, double axisX, double axisY, double axisZ, Vector point)
        {
            // Compute the normal vector at the intersection point
            return new Vector(
                2 * (point.X - center.X) / (axisX * axisX),
                2 * (point.Y - center.Y) / (axisY * axisY),
                2 * (point.Z - center.Z) / (axisZ * axisZ)
            ).Normalize();
        }

        private Vector NormalizeVector(Vector v)
        {
            return new Vector(v.X / SemiAxesLength.X, v.Y / SemiAxesLength.Y, v.Z / SemiAxesLength.Z);
        }

        public Tuple<double?, double?> FindIntersections(Line line)
        {
            var normDir = NormalizeVector(line.Dx);
            var normStartVec = NormalizeVector(line.X0 - Center);

            var aCoeff = normDir.Length2();
            var bCoeff = 2 * (normDir * normStartVec);
            var cCoeff = normStartVec.Length2() - Radius * Radius;

            var discriminant = bCoeff * bCoeff - 4 * aCoeff * cCoeff;

            if (discriminant < 1e-10)
            {
                return new Tuple<double?, double?>(null, null);
            }

            var t1 = (-bCoeff - Math.Sqrt(discriminant)) / (2 * aCoeff);
            var t2 = (-bCoeff + Math.Sqrt(discriminant)) / (2 * aCoeff);
            return new Tuple<double?, double?>(t1, t2);
        }

        public override Intersection GetIntersection(Line line, double minDist, double maxDist)
        {            
            double axisX = SemiAxesLength.X;
            double axisY = SemiAxesLength.Y;
            double axisZ = SemiAxesLength.Z;
            Vector center = Center;

            var (firstT, secondT) = FindIntersections(line);

            double selectedT;
            if (firstT == null && secondT == null)
            {
                return new Intersection(false, false, this, line, 0, null, this.Material, this.Color);
            }
            if (firstT == null)
            {
                selectedT = (double)secondT;
            }
            else if (secondT == null)
            {
                selectedT = (double)firstT;
            }
            else
            {
                selectedT = Math.Min((double)firstT, (double)secondT);
            }

            var isValid = selectedT >= minDist && selectedT <= maxDist;

            if (!isValid)
            {
                return new Intersection(false, false, this, line, 0, null, this.Material, this.Color);
            }

            return new Intersection(true, true, this, line, selectedT, ComputeNormal(center, axisX, axisY, axisZ, line.X0 + line.Dx * selectedT), this.Material, this.Color);
        }

    }
}
