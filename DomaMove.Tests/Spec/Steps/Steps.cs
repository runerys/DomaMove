using DomaMove.Doma;
using DomaMove.Engine;
using FakeItEasy;
using TechTalk.SpecFlow;

namespace DomaMove.Tests.Spec.Steps
{
    [Binding]
    public class StepDefinitions
    {
        private DomaConnection _connection;
        private ConnectionSettings _connectionSettings = new ConnectionSettings(Role.Source);

        private DomaClientFactory _clientFactory;

        [Given(@"A blank url field")]
        public void GivenABlankInputField()
        {
            _clientFactory = A.Fake<DomaClientFactory>();

            A.CallTo(() => _clientFactory.Create(null)).Returns(A.Fake<DOMAServicePortType>());

            var imageDownloader = A.Fake<ImageDownloader>();
           
            _connection = new DomaConnection(_clientFactory, imageDownloader, _connectionSettings);
        }

        [When(@"I enter ""(.*)""")]
        public void WhenIEnter(string p0)
        {
            _connectionSettings.Url = p0;
        }
      

        [Then(@"the connection check should be made to a webservice at ""(.*)""")]
        public void ThenTheConnectionCheckShouldBeMadeToAWebserviceAt(string p0)
        {
            _connection.TestConnection();

            A.CallTo(() => _clientFactory.Create(p0)).MustHaveHappened();
        }
    }
}
