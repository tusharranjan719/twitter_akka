# twitter_akka
The aim of the project is to clone twitter and its functionalities and test them using a simulator. We have covered almost all the basic features of twitter like secured login, tweet, retweet, follow etc. 
Tushar Ranjan – 45562694 Sankalp Pandey – 92878142
How to run:
1. Server:
a. Change directory to ‘Twitter_Server’ folder and run the file “Server.fs” using
command dotnet run 2. Client:
Change directory to ‘Twitter_Client’ folder and run the file “Client.fs” using:
a. dotnet run client – to run twitter with all its features.
b. dotnet run simulate – to test twitter
What is working: Bonus: (Finished)
We have implemented SHA256 encryption on the password entered by the user. The input (string) password is encrypted and then stored in database to tackle any kind of data breaches. While logging in, to match the password we first decrypt the string corresponding to the user id and then match it with the entered password.
Basic requirements: (Finished)
1. Client:
• Sign up – On running client user is asked to either login or signup, if he/she is new to
the program they will be given an option to create a unique user id, a password, and
a name.
• Login – User can login to their account using their unique id and password, once
logged in all the queried tweets will be displayed.
• Tweet – User can tweet anything with hashtags (#) and mentions (@).
• Retweet – Using the tweet id, user can retweet any tweet of their own or from
someone they follow.
• Follow – One can search for another user using their unique user id and can follow
them to see their tweets.
• Logout – To disconnect from the server or to login using a different account.
• History – User could see their previous tweets here, also if some retweeted their
tweet that will be visible here.
• Search hashtags – User has the option to search for tweets based on a hashtag
• Search mentions – User can also search tweets based on mentions. 2. Simulate:
• Based on number of users as input the program can simulate various conditions to test the functionality and connectivity. We have set various parameters that randomly perform actions and print the output.
3. Server:
• Server will continuously print its status every second, with information on requests
processed, total tweets in database, registered users and online users.
