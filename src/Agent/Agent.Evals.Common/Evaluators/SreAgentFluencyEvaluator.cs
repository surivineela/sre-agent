namespace Agent.Evals.Common.Evaluators;

public class SreAgentFluencyEvaluator : CustomRatingEvaluatorWithReasoning
{
    public const string SreAgentFluencyMetricName = "SreAgentFluency";

    protected override string GetSystemPrompt()
    {
        return "You are an SRE Agent chatting with a customer of Azure, and you want to be straight and to the point, rather than chitchat. Fluency measures the quality of individual sentences in the answer, and whether they are well-written and\r\ngrammatically correct. Consider the quality of individual sentences when evaluating fluency.\r\n\r\nGiven the question and answer, score the fluency of the answer between one to five stars using the\r\nfollowing rating scale:\r\nOne star: the answer completely lacks fluency\r\nTwo stars: the answer mostly lacks fluency\r\nThree stars: the answer is partially fluent\r\nFour stars: the answer is mostly fluent\r\nFive stars: the answer has perfect fluency\r\n\r\nThe rating value should always be an integer between 1 and 5. So the rating produced should be 1 or 2 or 3\r\nor 4 or 5.\r\n\r\nquestion: What did you have for breakfast today?\r\nanswer: Breakfast today, me eating cereal and orange juice very good.\r\nstars: 1\r\n\r\nquestion: How do you feel when you travel alone?\r\nanswer: Alone travel, nervous, but excited also. I feel adventure and like its time.\r\nstars: 2\r\n\r\nquestion: When was the last time you went on a family vacation?\r\nanswer: Last family vacation, it took place in last summer. We traveled to a beach destination, very fun.\r\nstars: 3\r\n\r\nquestion: What is your favorite thing about your job?\r\nanswer: My favorite aspect of my job is the chance to interact with diverse people. I am constantly\r\nlearning from their experiences and stories.\r\nstars: 4\r\n\r\nquestion: Can you describe your morning routine?\r\nanswer: Every morning, I wake up at 6 am, drink a glass of water, and do some light stretching. After that,\r\nI take a shower and get dressed for work. Then, I have a healthy breakfast, usually consisting of oatmeal\r\nand fruits, before leaving the house around 7:30 am.\r\nstars: 5\r\n\r\nquestion: {value}\r\nanswer: {renderedModelResponse}\r\nstars";
    }

    protected override string GetMetricName()
    {
        return SreAgentFluencyMetricName;
    }
}
