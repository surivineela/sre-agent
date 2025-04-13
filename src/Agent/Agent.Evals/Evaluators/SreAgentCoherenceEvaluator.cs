namespace Agent.Evals.Evaluators;

public class SreAgentCoherenceEvaluator : CustomRatingEvaluatorWithReasoning
{
    public const string SreAgentCoherenceMetricName = "SreAgentCoherence";

    protected override string GetSystemPrompt()
    {
        return "You are an SRE Agent chatting with a customer of Azure, and you want to be straight and to the point, rather than chitchat. Coherence of an answer is measured by how well all the sentences fit together and sound naturally as a\r\nwhole. Consider the overall quality of the answer when evaluating coherence.\r\n\r\nGiven the question and answer, score the coherence of the answer between one to five stars using the\r\nfollowing rating scale:\r\nOne star: the answer completely lacks coherence\r\nTwo stars: the answer mostly lacks coherence\r\nThree stars: the answer is partially coherent\r\nFour stars: the answer is mostly coherent\r\nFive stars: the answer has perfect coherency\r\n\r\nThe rating value should always be an integer between 1 and 5. So the rating produced should be 1 or 2 or 3\r\nor 4 or 5.\r\n\r\nquestion: What is your favorite indoor activity and why do you enjoy it?\r\nanswer: I like pizza. The sun is shining.\r\nstars: 1\r\n\r\nquestion: Can you describe your favorite movie without giving away any spoilers?\r\nanswer: It is a science fiction movie. There are dinosaurs. The actors eat cake. People must stop the\r\nvillain.\r\nstars: 2\r\n\r\nquestion: What are some benefits of regular exercise?\r\nanswer: Regular exercise improves your mood. A good workout also helps you sleep better. Trees are green.\r\nstars: 3\r\n\r\nquestion: How do you cope with stress in your daily life?\r\nanswer: I usually go for a walk to clear my head. Listening to music helps me relax as well. Stress is a\r\npart of life, but we can manage it through some activities.\r\nstars: 4\r\n\r\nquestion: What can you tell me about climate change and its effects on the environment?\r\nanswer: Climate change has far-reaching effects on the environment. Rising temperatures result in the\r\nmelting of polar ice caps, contributing to sea-level rise. Additionally, more frequent and severe weather\r\nevents, such as hurricanes and heatwaves, can cause disruption to ecosystems and human societies alike.\r\nstars: 5\r\n\r\nquestion: {value}\r\nanswer: {renderedModelResponse}\r\nstars:";
    }

    protected override string GetMetricName()
    {
        return SreAgentCoherenceMetricName;
    }
}
