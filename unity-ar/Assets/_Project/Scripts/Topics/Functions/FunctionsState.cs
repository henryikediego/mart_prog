namespace Loci.Topics.Functions
{
    /// <summary>Function parameters and return value, independent of Unity objects and AR.</summary>
    public sealed class FunctionsState
    {
        public const int DefaultA = 10;
        public const int DefaultB = 20;

        public int ParameterA { get; private set; }
        public int ParameterB { get; private set; }
        public int ReturnValue { get; private set; }
        public string CurrentFunction { get; private set; }

        public FunctionsState() => Reset();

        public void SetInput(int a, int b)
        {
            ParameterA = a;
            ParameterB = b;
            // The reference behavior deliberately retains the previous return value
            // until the learner calls Add, Subtract, or Multiply.
        }

        public bool Call(string functionName)
        {
            switch (functionName)
            {
                case "Add":
                    CurrentFunction = "add";
                    ReturnValue = ParameterA + ParameterB;
                    return true;
                case "Subtract":
                    CurrentFunction = "subtract";
                    ReturnValue = ParameterA - ParameterB;
                    return true;
                case "Multiply":
                    CurrentFunction = "multiply";
                    ReturnValue = ParameterA * ParameterB;
                    return true;
                default:
                    return false;
            }
        }

        public void Reset()
        {
            ParameterA = DefaultA;
            ParameterB = DefaultB;
            CurrentFunction = "add";
            ReturnValue = DefaultA + DefaultB;
        }
    }
}
