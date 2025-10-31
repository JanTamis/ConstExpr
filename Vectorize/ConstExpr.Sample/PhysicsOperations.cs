using ConstExpr.Core.Attributes;
using ConstExpr.Core.Enumerators;
using System;

namespace ConstExpr.SourceGenerator.Sample;

[ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
public static class PhysicsOperations
{
	public static double ProjectileMaxHeight(double initialVelocity, double launchAngle, double gravity)
	{
		if (gravity <= 0)
		{
			throw new ArgumentException("Gravity must be positive");
		}

		var angleRad = launchAngle * Math.PI / 180.0;
		var verticalVelocity = initialVelocity * Math.Sin(angleRad);
		return (verticalVelocity * verticalVelocity) / (2 * gravity);
	}

	// Additional physics operations
	public static double ProjectileRange(double initialVelocity, double launchAngle, double gravity)
	{
		if (gravity <= 0)
		{
			throw new ArgumentException("Gravity must be positive");
		}

		var angleRad = launchAngle * Math.PI / 180.0;
		return (initialVelocity * initialVelocity * Math.Sin(2 * angleRad)) / gravity;
	}

	public static double ProjectileTimeOfFlight(double initialVelocity, double launchAngle, double gravity)
	{
		if (gravity <= 0)
		{
			throw new ArgumentException("Gravity must be positive");
		}

		var angleRad = launchAngle * Math.PI / 180.0;
		var verticalVelocity = initialVelocity * Math.Sin(angleRad);
		return (2 * verticalVelocity) / gravity;
	}

	public static double KineticEnergy(double mass, double velocity)
	{
		if (mass < 0)
		{
			throw new ArgumentException("Mass cannot be negative");
		}

		return 0.5 * mass * velocity * velocity;
	}

	public static double PotentialEnergy(double mass, double height, double gravity)
	{
		if (mass < 0)
		{
			throw new ArgumentException("Mass cannot be negative");
		}

		return mass * gravity * height;
	}

	public static double Force(double mass, double acceleration)
	{
		return mass * acceleration;
	}

	public static double Momentum(double mass, double velocity)
	{
		return mass * velocity;
	}

	public static double Work(double force, double distance, double angleInDegrees)
	{
		var angleRad = angleInDegrees * Math.PI / 180.0;
		return force * distance * Math.Cos(angleRad);
	}

	public static double Power(double work, double time)
	{
		if (time == 0)
		{
			throw new ArgumentException("Time cannot be zero");
		}

		return work / time;
	}

	public static double Impulse(double force, double time)
	{
		return force * time;
	}

	public static double CentripetalAcceleration(double velocity, double radius)
	{
		if (radius == 0)
		{
			throw new ArgumentException("Radius cannot be zero");
		}

		return (velocity * velocity) / radius;
	}

	public static double CentripetalForce(double mass, double velocity, double radius)
	{
		if (radius == 0)
		{
			throw new ArgumentException("Radius cannot be zero");
		}

		return (mass * velocity * velocity) / radius;
	}

	public static double Frequency(double period)
	{
		if (period == 0)
		{
			throw new ArgumentException("Period cannot be zero");
		}

		return 1.0 / period;
	}

	public static double Period(double frequency)
	{
		if (frequency == 0)
		{
			throw new ArgumentException("Frequency cannot be zero");
		}

		return 1.0 / frequency;
	}

	public static double AngularVelocity(double linearVelocity, double radius)
	{
		if (radius == 0)
		{
			throw new ArgumentException("Radius cannot be zero");
		}

		return linearVelocity / radius;
	}

	public static double Wavelength(double speedOfWave, double frequency)
	{
		if (frequency == 0)
		{
			throw new ArgumentException("Frequency cannot be zero");
		}

		return speedOfWave / frequency;
	}

	public static double DopplerEffect(double sourceFrequency, double speedOfSound, double sourceVelocity, double observerVelocity)
	{
		return sourceFrequency * (speedOfSound + observerVelocity) / (speedOfSound - sourceVelocity);
	}

	public static double ElasticPotentialEnergy(double springConstant, double displacement)
	{
		return 0.5 * springConstant * displacement * displacement;
	}

	public static double Pressure(double force, double area)
	{
		if (area == 0)
		{
			throw new ArgumentException("Area cannot be zero");
		}

		return force / area;
	}

	public static double Density(double mass, double volume)
	{
		if (volume == 0)
		{
			throw new ArgumentException("Volume cannot be zero");
		}

		return mass / volume;
	}

	public static double EscapeVelocity(double gravitationalConstant, double mass, double radius)
	{
		if (radius == 0)
		{
			throw new ArgumentException("Radius cannot be zero");
		}

		return Math.Sqrt((2 * gravitationalConstant * mass) / radius);
	}

	public static double RelativisticMass(double restMass, double velocity, double speedOfLight)
	{
		var beta = velocity / speedOfLight;
		return restMass / Math.Sqrt(1 - beta * beta);
	}

	public static double SchwarzschildRadius(double mass, double gravitationalConstant, double speedOfLight)
	{
		return (2 * gravitationalConstant * mass) / (speedOfLight * speedOfLight);
	}
}

