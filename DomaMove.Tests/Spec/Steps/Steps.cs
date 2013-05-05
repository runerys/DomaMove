using TechTalk.SpecFlow;

namespace DomaMove.Tests.Spec.Steps
{
    [Binding]
    public class StepDefinitions
    {
        [Given(@"A blank input field")]
        public void GivenABlankInputField()
        {
            ScenarioContext.Current.Pending();
        }

        [When(@"I enter ""(.*)""")]
        public void WhenIEnter(string p0)
        {
            ScenarioContext.Current.Pending();
        }

        [When(@"valid username and password")]
        public void WhenValidUsernameAndPassword()
        {
            ScenarioContext.Current.Pending();
        }

        [Then(@"the connection check should be made to a webservice at ""(.*)""")]
        public void ThenTheConnectionCheckShouldBeMadeToAWebserviceAt(string p0)
        {
            ScenarioContext.Current.Pending();
        }
    }
}
