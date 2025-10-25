using ConstExpr.Core.Attributes;
using System;

namespace ConstExpr.SourceGenerator.Sample;

[ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
public static class GeometryOperations
{
	public static double TriangleArea(double sideA, double sideB, double angleC)
	{
		if (sideA <= 0 || sideB <= 0)
		{
			throw new ArgumentException("Sides must be positive");
		}

		var angleRad = angleC * Math.PI / 180.0;
		return 0.5 * sideA * sideB * Math.Sin(angleRad);
	}

	// Additional geometry operations
	public static double CircleArea(double radius)
	{
		if (radius < 0)
		{
			throw new ArgumentException("Radius cannot be negative");
		}

		return Math.PI * radius * radius;
	}

	public static double CircleCircumference(double radius)
	{
		if (radius < 0)
		{
			throw new ArgumentException("Radius cannot be negative");
		}

		return 2 * Math.PI * radius;
	}

	public static double RectangleArea(double width, double height)
	{
		if (width < 0 || height < 0)
		{
			throw new ArgumentException("Width and height cannot be negative");
		}

		return width * height;
	}

	public static double RectanglePerimeter(double width, double height)
	{
		if (width < 0 || height < 0)
		{
			throw new ArgumentException("Width and height cannot be negative");
		}

		return 2 * (width + height);
	}

	public static double SphereVolume(double radius)
	{
		if (radius < 0)
		{
			throw new ArgumentException("Radius cannot be negative");
		}

		return (4.0 / 3.0) * Math.PI * radius * radius * radius;
	}

	public static double SphereSurfaceArea(double radius)
	{
		if (radius < 0)
		{
			throw new ArgumentException("Radius cannot be negative");
		}

		return 4 * Math.PI * radius * radius;
	}

	public static double CylinderVolume(double radius, double height)
	{
		if (radius < 0 || height < 0)
		{
			throw new ArgumentException("Radius and height cannot be negative");
		}

		return Math.PI * radius * radius * height;
	}

	public static double CylinderSurfaceArea(double radius, double height)
	{
		if (radius < 0 || height < 0)
		{
			throw new ArgumentException("Radius and height cannot be negative");
		}

		return 2 * Math.PI * radius * height + 2 * Math.PI * radius * radius;
	}

	public static double ConeVolume(double radius, double height)
	{
		if (radius < 0 || height < 0)
		{
			throw new ArgumentException("Radius and height cannot be negative");
		}

		return (1.0 / 3.0) * Math.PI * radius * radius * height;
	}

	public static double TriangleAreaHeron(double sideA, double sideB, double sideC)
	{
		if (sideA <= 0 || sideB <= 0 || sideC <= 0)
		{
			throw new ArgumentException("All sides must be positive");
		}

		if (sideA + sideB <= sideC || sideA + sideC <= sideB || sideB + sideC <= sideA)
		{
			throw new ArgumentException("Invalid triangle sides");
		}

		var s = (sideA + sideB + sideC) / 2.0;
		return Math.Sqrt(s * (s - sideA) * (s - sideB) * (s - sideC));
	}

	public static double Distance2D(double x1, double y1, double x2, double y2)
	{
		var dx = x2 - x1;
		var dy = y2 - y1;
		return Math.Sqrt(dx * dx + dy * dy);
	}

	public static double Distance3D(double x1, double y1, double z1, double x2, double y2, double z2)
	{
		var dx = x2 - x1;
		var dy = y2 - y1;
		var dz = z2 - z1;
		return Math.Sqrt(dx * dx + dy * dy + dz * dz);
	}

	public static double ManhattanDistance2D(double x1, double y1, double x2, double y2)
	{
		return Math.Abs(x2 - x1) + Math.Abs(y2 - y1);
	}

	public static double PolygonArea(params double[] coordinates)
	{
		if (coordinates.Length < 6 || coordinates.Length % 2 != 0)
		{
			throw new ArgumentException("Need at least 3 points (6 coordinates) and even number of coordinates");
		}

		var area = 0.0;
		var n = coordinates.Length / 2;

		for (var i = 0; i < n; i++)
		{
			var j = (i + 1) % n;
			var x1 = coordinates[i * 2];
			var y1 = coordinates[i * 2 + 1];
			var x2 = coordinates[j * 2];
			var y2 = coordinates[j * 2 + 1];
			area += x1 * y2 - x2 * y1;
		}

		return Math.Abs(area) / 2.0;
	}

	public static (double x, double y) MidPoint2D(double x1, double y1, double x2, double y2)
	{
		return ((x1 + x2) / 2.0, (y1 + y2) / 2.0);
	}

	public static double AngleBetweenVectors2D(double x1, double y1, double x2, double y2)
	{
		var dotProduct = x1 * x2 + y1 * y2;
		var mag1 = Math.Sqrt(x1 * x1 + y1 * y1);
		var mag2 = Math.Sqrt(x2 * x2 + y2 * y2);

		if (mag1 == 0 || mag2 == 0)
		{
			throw new ArgumentException("Zero-length vector");
		}

		var cosAngle = dotProduct / (mag1 * mag2);
		cosAngle = Math.Max(-1.0, Math.Min(1.0, cosAngle));

		return Math.Acos(cosAngle) * 180.0 / Math.PI;
	}

	public static bool IsPointInRectangle(double px, double py, double rectX, double rectY, double width, double height)
	{
		return px >= rectX && px <= rectX + width && py >= rectY && py <= rectY + height;
	}

	public static bool IsPointInCircle(double px, double py, double centerX, double centerY, double radius)
	{
		var dx = px - centerX;
		var dy = py - centerY;
		return dx * dx + dy * dy <= radius * radius;
	}

	public static double EllipseArea(double semiMajorAxis, double semiMinorAxis)
	{
		if (semiMajorAxis < 0 || semiMinorAxis < 0)
		{
			throw new ArgumentException("Axes cannot be negative");
		}

		return Math.PI * semiMajorAxis * semiMinorAxis;
	}

	public static double TrapezoidArea(double base1, double base2, double height)
	{
		if (base1 < 0 || base2 < 0 || height < 0)
		{
			throw new ArgumentException("Dimensions cannot be negative");
		}

		return (base1 + base2) * height / 2.0;
	}
}

