// Samples.cs - Samples pane text, written for SoftCalc (SPEC 2, not the original's wording)

namespace SoftCalc.UI;

internal static class Samples
{
	public const string Infix =
		"56+34    add two numbers\r\n" +
		"x=5    store 5 in the variable x;  2x  then means 2*x\r\n" +
		"f(x)=x^2    define a function;  f(3)  calls it\r\n" +
		"1 [in->cm]    convert units;  now [longdatetime]  formats a date\r\n" +
		"answer, $1, $2    the last result, the results before it";

	public const string Rpn =
		"56 34 +    add two numbers (operands first, then the operator)\r\n" +
		"5 sqrt    square root of 5\r\n" +
		"1 2 3 + *    (2+3)*1\r\n" +
		"x=5    definitions keep the normal form in RPN mode";
}
