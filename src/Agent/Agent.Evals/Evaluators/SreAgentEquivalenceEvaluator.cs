namespace Agent.Evals.Evaluators;

public class SreAgentEquivalenceEvaluator : CustomRatingEvaluatorWithReasoning
{
    public const string SreAgentEquivalenceMetricName = "SreAgentEquivalence";

    private readonly string _groundedTruth;
    private readonly string _question;
    private readonly string _response;

    public SreAgentEquivalenceEvaluator(string groundedTruth, string question, string response)
    {
        _groundedTruth = groundedTruth;
        _question = question;
        _response = response;
    }

    protected override string GetSystemPrompt()
    {
        return $"You are an SRE Agent chatting with a customer of Azure, and you want to be straight and to the point, rather than chitchat. Equivalence, as a metric, measures the similarity between the predicted answer and the correct answer. If\r\nthe information and content in the predicted answer is similar or equivalent to the correct answer, then\r\nthe value of the Equivalence metric should be high, else it should be low.\r\n\r\nGiven the question, correct answer, and predicted answer, determine the value of Equivalence metric using\r\nthe following rating scale:\r\nOne star: the predicted answer is not at all similar to the correct answer\r\nTwo stars: the predicted answer is mostly not similar to the correct answer\r\nThree stars: the predicted answer is somewhat similar to the correct answer\r\nFour stars: the predicted answer is mostly similar to the correct answer\r\nFive stars: the predicted answer is completely similar to the correct answer\r\n\r\nThe rating value should always be an integer between 1 and 5. So the rating produced should be 1 or 2 or 3\r\nor 4 or 5.\r\n\r\nThe examples below show the Equivalence score for a question, a correct answer, and a predicted answer.\r\n\r\nquestion: What is the role of ribosomes?\r\ncorrect answer: Ribosomes are cellular structures responsible for protein synthesis. They interpret the\r\ngenetic information carried by messenger RNA (mRNA) and use it to assemble amino acids into proteins.\r\npredicted answer: Ribosomes participate in carbohydrate breakdown by removing nutrients from complex sugar\r\nmolecules.\r\nstars: 1\r\n\r\nquestion: Why did the Titanic sink?\r\ncorrect answer: The Titanic sank after it struck an iceberg during its maiden voyage in 1912. The impact\r\ncaused the ship's hull to breach, allowing water to flood into the vessel. The ship's design, lifeboat\r\nshortage, and lack of timely rescue efforts contributed to the tragic loss of life.\r\npredicted answer: The sinking of the Titanic was a result of a large iceberg collision. This caused the\r\nship to take on water and eventually sink, leading to the death of many passengers due to a shortage of\r\nlifeboats and insufficient rescue attempts.\r\nstars: 2\r\n\r\nquestion: What causes seasons on Earth?\r\ncorrect answer: Seasons on Earth are caused by the tilt of the Earth's axis and its revolution around the\r\nSun. As the Earth orbits the Sun, the tilt causes different parts of the planet to receive varying amounts\r\nof sunlight, resulting in changes in temperature and weather patterns.\r\npredicted answer: Seasons occur because of the Earth's rotation and its elliptical orbit around the Sun.\r\nThe tilt of the Earth's axis causes regions to be subjected to different sunlight intensities, which leads\r\nto temperature fluctuations and alternating weather conditions.\r\nstars: 3\r\n\r\nquestion: How does photosynthesis work?\r\ncorrect answer: Photosynthesis is a process by which green plants and some other organisms convert light\r\nenergy into chemical energy. This occurs as light is absorbed by chlorophyll molecules, and then carbon\r\ndioxide and water are converted into glucose and oxygen through a series of reactions.\r\npredicted answer: In photosynthesis, sunlight is transformed into nutrients by plants and certain\r\nmicroorganisms. Light is captured by chlorophyll molecules, followed by the conversion of carbon dioxide\r\nand water into sugar and oxygen through multiple reactions.\r\nstars: 4\r\n\r\nquestion: What are the health benefits of regular exercise?\r\ncorrect answer: Regular exercise can help maintain a healthy weight, increase muscle and bone strength, and\r\nreduce the risk of chronic diseases. It also promotes mental well-being by reducing stress and improving\r\noverall mood.\r\npredicted answer: Routine physical activity can contribute to maintaining ideal body weight, enhancing\r\nmuscle and bone strength, and preventing chronic illnesses. In addition, it supports mental health by\r\nalleviating stress and augmenting general mood.\r\nstars: 5\r\n\r\nquestion: {_question}\r\ncorrect answer:{_groundedTruth}\r\npredicted answer: {_response}\r\nstars:";
    }

    protected override string GetMetricName()
    {
        return SreAgentEquivalenceMetricName;
    }
}
