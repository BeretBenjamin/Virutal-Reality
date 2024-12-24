using System;

namespace rt
{
    class RayTracer
    {
        private Geometry[] geometries;
        private Light[] lights;

        public RayTracer(Geometry[] geometries, Light[] lights)
        {
            this.geometries = geometries;
            this.lights = lights;
        }

        private double ImageToViewPlane(int n, int imgSize, double viewPlaneSize)
        {
            return -n * viewPlaneSize / imgSize + viewPlaneSize / 2;
        }

        private Intersection FindFirstIntersection(Line ray, double minDist, double maxDist)
        {
            var intersection = Intersection.NONE;

            foreach (var geometry in geometries)
            {
                var intr = geometry.GetIntersection(ray, minDist, maxDist);

                if (!intr.Valid || !intr.Visible) continue;

                if (!intersection.Valid || !intersection.Visible)
                {
                    intersection = intr;
                }
                else if (intr.T < intersection.T)
                {
                    intersection = intr;
                }
            }

            return intersection;
        }

        private bool IsLit(Vector point, Light light)
        {
            // ADD CODE HERE:
            Line ray = new Line(point, light.Position);
            var maxDist = (light.Position - point).Length() + 1.0d; // epsilon value
            foreach (var geometry in geometries)
            {
                if (geometry is RawCtMask)
                    continue; // Skip RawCtMask objects
                Intersection intersect = geometry.GetIntersection(ray, 0.01d, maxDist);
                if(intersect.Visible)
                {
                    return false;
                }
            }
            return true;
        }

        public void Render(Camera camera, int width, int height, string filename)
        {
            var background = new Color(0.2, 0.2, 0.2, 1.0);
            var viewParallel = (camera.Up ^ camera.Direction).Normalize(); // Perpendicular vector in the camera direction

            var image = new Image(width, height);

            var cameraPosition = camera.Position;
            var viewPlaneCenter = camera.Direction * camera.ViewPlaneDistance;

            for (var i = 0; i < width; i++)
            {
                for (var j = 0; j < height; j++)
                {
                    // ADD CODE HERE
                    var finalPixel = new Color();
                    Vector rayDirection = cameraPosition
                                        + viewPlaneCenter
                                        + viewParallel * ImageToViewPlane(i, width, camera.ViewPlaneWidth) // adjust horizontally based on the pixel x-coord
                                        + camera.Up * ImageToViewPlane(j, height, camera.ViewPlaneHeight); //adjust vertically based on the pixel y-coord

                    Line ray = new Line(cameraPosition, rayDirection);
                    Intersection intersect = FindFirstIntersection(ray, camera.FrontPlaneDistance, camera.BackPlaneDistance);
                    var finalColor = new Color();
                    if (intersect.Valid && intersect.Visible)
                    {                       
                        foreach (var light in lights)
                        {
                            var color = intersect.Material.Ambient * light.Ambient;

                            if (IsLit(intersect.Position, light))
                            {
                                Vector N = intersect.Normal;
                                Vector T = (light.Position - intersect.Position).Normalize(); // light direction
                                Vector E = (camera.Position - intersect.Position).Normalize(); // camera direction
                                Vector R = (N * (N * T) * 2 - T).Normalize(); // reflection direction

                                // check if light hits the surface
                                if (N * T > 0.0d)
                                {
                                    color += intersect.Material.Diffuse * light.Diffuse * (N * T);
                                }
                                // check if refelection is towards the camera
                                if (E * R > 0.0d)
                                {
                                    color += intersect.Material.Specular * light.Specular * Math.Pow(E * R, intersect.Material.Shininess);
                                }

                                color *= light.Intensity;
                            }

                            finalColor += color;
                        }

                        // Clamp the final color values to ensure they are within the range [0, 1]
                        finalColor.Red = Math.Min(finalColor.Red, 1.0);
                        finalColor.Green = Math.Min(finalColor.Green, 1.0);
                        finalColor.Blue = Math.Min(finalColor.Blue, 1.0);

                        image.SetPixel(i, j, finalColor);
                    }
                    else
                    {
                        image.SetPixel(i, j, background);
                    }
                }
            }

            image.Store(filename);
        }

    }
}