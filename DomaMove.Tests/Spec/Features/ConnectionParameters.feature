Feature: ConnectionParameters
	In order to avoid problems on connection parameters
	As a user
	I want to be able to type in a url both with and without "/webservice.php" in the end

@mytag
Scenario: Enter Doma uri ending with slash
	Given A blank url field	
	When I enter "http://mydoma.com/"
	Then the connection check should be made to a webservice at "http://mydoma.com/webservice.php"

Scenario: Enter Doma uri ending without slash
	Given A blank url field	
	When I enter "http://mydoma.com"
	Then the connection check should be made to a webservice at "http://mydoma.com/webservice.php"

Scenario: Enter Doma uri ending with webservice
	Given A blank url field	
	When I enter "http://mydoma.com/webservice.php"	
	Then the connection check should be made to a webservice at "http://mydoma.com/webservice.php"