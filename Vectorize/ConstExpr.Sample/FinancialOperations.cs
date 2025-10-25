using ConstExpr.Core.Attributes;
using System;

namespace ConstExpr.SourceGenerator.Sample;

[ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
public static class FinancialOperations
{
	public static double CompoundInterest(double principal, double rate, int timesCompounded, double years)
	{
		if (principal < 0 || rate < 0 || timesCompounded <= 0 || years < 0)
		{
			throw new ArgumentException("Invalid input parameters");
		}

		return principal * Math.Pow(1.0 + rate / timesCompounded, timesCompounded * years);
	}

	// Additional financial operations
	public static double SimpleInterest(double principal, double rate, double time)
	{
		if (principal < 0 || rate < 0 || time < 0)
		{
			throw new ArgumentException("Invalid input parameters");
		}

		return principal * rate * time;
	}

	public static double FutureValue(double presentValue, double rate, int periods)
	{
		if (periods < 0)
		{
			throw new ArgumentException("Periods cannot be negative");
		}

		return presentValue * Math.Pow(1.0 + rate, periods);
	}

	public static double PresentValue(double futureValue, double rate, int periods)
	{
		if (periods < 0)
		{
			throw new ArgumentException("Periods cannot be negative");
		}

		return futureValue / Math.Pow(1.0 + rate, periods);
	}

	public static double MonthlyMortgagePayment(double principal, double annualRate, int years)
	{
		if (principal <= 0 || annualRate < 0 || years <= 0)
		{
			throw new ArgumentException("Invalid input parameters");
		}

		if (annualRate == 0)
		{
			return principal / (years * 12);
		}

		var monthlyRate = annualRate / 12;
		var numberOfPayments = years * 12;

		return principal * (monthlyRate * Math.Pow(1 + monthlyRate, numberOfPayments)) /
		       (Math.Pow(1 + monthlyRate, numberOfPayments) - 1);
	}

	public static double LoanPayment(double principal, double rate, int periods)
	{
		if (principal <= 0 || rate < 0 || periods <= 0)
		{
			throw new ArgumentException("Invalid input parameters");
		}

		if (rate == 0)
		{
			return principal / periods;
		}

		return principal * (rate * Math.Pow(1 + rate, periods)) / (Math.Pow(1 + rate, periods) - 1);
	}

	public static double NetPresentValue(double rate, params double[] cashFlows)
	{
		if (cashFlows == null || cashFlows.Length == 0)
		{
			throw new ArgumentException("Cash flows cannot be null or empty");
		}

		var npv = 0.0;

		for (var i = 0; i < cashFlows.Length; i++)
		{
			npv += cashFlows[i] / Math.Pow(1 + rate, i);
		}

		return npv;
	}

	public static double InternalRateOfReturn(params double[] cashFlows)
	{
		if (cashFlows == null || cashFlows.Length < 2)
		{
			throw new ArgumentException("Need at least 2 cash flows");
		}

		var rate = 0.1;
		var tolerance = 0.0001;
		var maxIterations = 100;

		for (var iteration = 0; iteration < maxIterations; iteration++)
		{
			var npv = 0.0;
			var derivative = 0.0;

			for (var i = 0; i < cashFlows.Length; i++)
			{
				var discountFactor = Math.Pow(1 + rate, i);
				npv += cashFlows[i] / discountFactor;
				derivative -= i * cashFlows[i] / (discountFactor * (1 + rate));
			}

			if (Math.Abs(npv) < tolerance)
			{
				return rate;
			}

			if (Math.Abs(derivative) < tolerance)
			{
				break;
			}

			rate -= npv / derivative;
		}

		return rate;
	}

	public static double AnnuityPayment(double rate, int periods, double presentValue)
	{
		if (periods <= 0)
		{
			throw new ArgumentException("Periods must be positive");
		}

		if (rate == 0)
		{
			return presentValue / periods;
		}

		return presentValue * rate / (1 - Math.Pow(1 + rate, -periods));
	}

	public static double AnnuityFutureValue(double payment, double rate, int periods)
	{
		if (periods <= 0)
		{
			throw new ArgumentException("Periods must be positive");
		}

		if (rate == 0)
		{
			return payment * periods;
		}

		return payment * ((Math.Pow(1 + rate, periods) - 1) / rate);
	}

	public static double EffectiveAnnualRate(double nominalRate, int compoundingPeriods)
	{
		if (compoundingPeriods <= 0)
		{
			throw new ArgumentException("Compounding periods must be positive");
		}

		return Math.Pow(1 + nominalRate / compoundingPeriods, compoundingPeriods) - 1;
	}

	public static double BreakEvenPoint(double fixedCosts, double pricePerUnit, double variableCostPerUnit)
	{
		var contributionMargin = pricePerUnit - variableCostPerUnit;

		if (contributionMargin <= 0)
		{
			throw new ArgumentException("Price must be greater than variable cost");
		}

		return fixedCosts / contributionMargin;
	}

	public static double ReturnOnInvestment(double gain, double cost)
	{
		if (cost == 0)
		{
			throw new ArgumentException("Cost cannot be zero");
		}

		return (gain - cost) / cost;
	}

	public static double ProfitMargin(double revenue, double cost)
	{
		if (revenue == 0)
		{
			throw new ArgumentException("Revenue cannot be zero");
		}

		return (revenue - cost) / revenue;
	}

	public static double DividendYield(double annualDividend, double stockPrice)
	{
		if (stockPrice == 0)
		{
			throw new ArgumentException("Stock price cannot be zero");
		}

		return annualDividend / stockPrice;
	}

	public static double EarningsPerShare(double netIncome, double shares)
	{
		if (shares == 0)
		{
			throw new ArgumentException("Shares cannot be zero");
		}

		return netIncome / shares;
	}

	public static double PriceToEarningsRatio(double stockPrice, double earningsPerShare)
	{
		if (earningsPerShare == 0)
		{
			throw new ArgumentException("Earnings per share cannot be zero");
		}

		return stockPrice / earningsPerShare;
	}

	public static double Depreciation(double cost, double salvageValue, int usefulLife)
	{
		if (usefulLife <= 0)
		{
			throw new ArgumentException("Useful life must be positive");
		}

		return (cost - salvageValue) / usefulLife;
	}

	public static double TaxAmount(double income, double taxRate)
	{
		if (taxRate < 0 || taxRate > 1)
		{
			throw new ArgumentException("Tax rate must be between 0 and 1");
		}

		return income * taxRate;
	}
}

