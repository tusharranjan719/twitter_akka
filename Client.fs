open System
open System.Security.Cryptography
open System.Globalization
open System.Text
open System.Collections.Generic
open Akka.Actor
open Akka.FSharp
open System.Diagnostics

open FSharp.Data
open FSharp.Data.JsonExtensions
open FSharp.Json

type reply = {
    Req : string
    Type : string
    Report : string
    Description : string option
}

type request_mssg = {
    Req : string
    personId : int
    Name : string
    Key : string option
}

type tweet = {
    Req : string
    personId : int
    TId : string
    Time : DateTime
    Message_body : string
    HashTag : string
    Mention : int
    No_of_retweets : int
}

type reply_mssg = {
    Req : string
    Type : string
    Report : int
    tweet : tweet
}

type seconday_info = {
    Req : string
    personId : int 
    SourceId : int
}

type seconday_reply_mssg = {
    Req : string
    Type : string
    Receiver_Id : int
    Follower : int[]
    Source : int[]
}

type ConnectInfo = {
    Req : string
    personId : int
}

type QueryInfo = {
    Req : string
    personId : int
    HashTag : string
}

type RetweetInfo = {
    Req: string
    personId : int
    Receiver_Id : int
    Retweet_Key : string
}

let config =
    Configuration.parse
        @"akka {
            log-dead-letters = off
            log-dead-letters-during-shutdown = off
            log-config-on-start = off
            actor.provider = remote
            remote.helios.tcp {
                hostname = localhost
                port = 0
            }
        }"

let system = System.create "Simulator" config
let serverActor = system.ActorSelection("akka.tcp://TwitterEngine@localhost:9001/user/TWServer")
let mainTimer = Stopwatch()
let passmanager = new Dictionary<int, string>()
let mutable Simulation_flag = false

type Report_check =
| Success
| Failure
| Wait
| TimerOver
let mutable (loginSuccess:Report_check) = Wait

let BossNode (singleUserFlag: bool) (clientMailbox:Actor<string>) =
    let mutable actorName = "User" + clientMailbox.Self.Path.Name
    let mutable actorId = 
        match (Int32.TryParse(clientMailbox.Self.Path.Name)) with
        | (true, value) -> value
        | (false, _) -> 0
    
    let selfReferenceActor = clientMailbox.Self
   
    let mutable userOnline = false
    let mutable canDebug = false // developer use, break this limit
    let mutable userOffline = true
    let mutable query = false

    let rec loop() = actor {
        let! (message: string) = clientMailbox.Receive()
        let  mssg_in = JsonValue.Parse(message)
        let  reqModel = mssg_in?Req.AsString()
        userOffline <- (not userOnline) && (not canDebug)
        match reqModel with
            | "RegisterUser" ->
                if Simulation_flag then
                    let regMsg:request_mssg = { 
                        Req = reqModel ; 
                        personId = actorId ; 
                        Name = actorName ; 
                        Key = Some (actorName+"Key") ;
                    }
                    serverActor <! (Json.serialize regMsg)
                else
                    serverActor <! message
                
                if singleUserFlag then 
                    mainTimer.Restart()

            | "SendMessage" ->
                if userOffline then
                    printfn "[%s] Failed to send tweet" actorName
                else   

                    if Simulation_flag then
                        serverActor <! message
                    else
                        serverActor <! message

                if singleUserFlag then 
                    mainTimer.Restart()

            | "Retweet" ->
                if userOffline then
                    printfn "[%s] Failed to send tweet" actorName
                else

                    if Simulation_flag then
                        serverActor <! message
                    else
                        serverActor <! message

                if singleUserFlag then 
                    mainTimer.Restart()

            | "Follow" ->
                if userOffline then
                    printfn "[%s] Action failed, seems like you are not connected to the server" actorName
                else

                    if Simulation_flag then
                        serverActor <! message
                    else
                        serverActor <! message
                
                if singleUserFlag then 
                    mainTimer.Restart()

            | "Link" ->
                if userOnline then
                    if Simulation_flag then
                        selfReferenceActor <! """{"Req":"History", "personId":"""+"\""+ actorId.ToString() + "\"}"
                    else
                        
                        serverActor <! message
                else
                    if Simulation_flag then
                        serverActor <! message
            
                    else
                        serverActor <! message
                
                if singleUserFlag then 
                    mainTimer.Restart()

            | "UnLink" ->
                userOnline <- false

                if Simulation_flag then
                    serverActor <! message
                else

                    serverActor <! message
                
                if singleUserFlag then 
                    mainTimer.Restart()

            | "History" | "Subscribe" | "Mention" | "Tag" ->
                if userOffline then
                    printfn "[%s] Action failed, seems like you are not connected to the server" actorName
                else
                    if query then
                        printfn "[%s] Action failed, seems like you are not connected to the server" actorName
                    else
                        query <- true

                        if reqModel = "Tag" then
                            if Simulation_flag then
                                serverActor <! message
                            else
                                serverActor <! message
                        else
                            if Simulation_flag then
                                serverActor <! message
                            else
                                serverActor <! message
                    
                    if singleUserFlag then 
                        mainTimer.Restart()
                
            | "Replymssg" ->
                let replyType = mssg_in?Type.AsString()
                match replyType with
                    | "RegisterUser" ->
                        let report = mssg_in?Report.AsString()
                        let userRegistrationid = mssg_in?Description.AsString() |> int
                        if report = "Success" then
                            if Simulation_flag then 
                                printfn "[%s] Successfully registered" actorName
                            else
                                loginSuccess <- Success
                            let (connectMsg:ConnectInfo) = {
                                Req = "Link" ;
                                personId = userRegistrationid ;
                            }
                            serverActor <! (Json.serialize connectMsg)
                            mainTimer.Restart()
                        else
                            if Simulation_flag then 
                                printfn "User with id %s already exists)" actorName
                            else
                                loginSuccess <- Failure
                        

                    | "Follow" ->
                        let report = mssg_in?Report.AsString()
                        if report = "Success" then
                            printfn "[%s] Subscirbe done!" actorName
                        else
                            printfn "[%s] Follow failed!" actorName

                    | "SendMessage" ->
                        let report = mssg_in?Report.AsString()
                        if report = "Success" then
                            printfn "[%s] %s" actorName (mssg_in?Description.AsString())
                        else
                            printfn "[%s] %s" actorName (mssg_in?Description.AsString())

                    | "Link" ->
                        let report = mssg_in?Report.AsString()
                        if report = "Success" then
                            userOnline <- true
                            if Simulation_flag then 
                                printfn "[%s] User%s successfully connected to server" actorName (mssg_in?Description.AsString())
                            else
                                loginSuccess <- Success
                            let (queryMsg:QueryInfo) = {
                                Req = "History" ;
                                personId = (mssg_in?Description.AsString()|> int) ;
                                HashTag = "" ;
                            }
                            serverActor <! (Json.serialize queryMsg)
                            mainTimer.Restart()
                        else
                            if Simulation_flag then 
                                printfn "[%s] Connection failed, %s" actorName (mssg_in?Description.AsString())
                            else
                                loginSuccess <- Failure

                    | "UnLink" ->
                        if Simulation_flag then 
                            printfn "[%s] User%s disconnected " actorName (mssg_in?Description.AsString())
                        else
                            loginSuccess <- Success
                    | "History" ->
                        let report = mssg_in?Report.AsString()
                        if report = "Success" then
                            query <- false
                            printfn "\n[%s] %s" actorName (mssg_in?Description.AsString())
                        else if report = "NoTweet" then
                            query <- false
                            printfn "[%s] %s" actorName (mssg_in?Description.AsString())
                        else
                            printfn "[%s] Something went wrong" actorName

                    | "ReadTweet" ->
                        if not Simulation_flag then
                            let tweetReplyInfo = (Json.deserialize<reply_mssg> message)
                            let tweetInfo = tweetReplyInfo.tweet
                            printfn "Index: %i      Time: %s" (tweetReplyInfo.Report) (tweetInfo.Time.ToString())
                            printfn "Author: User%i" (tweetInfo.personId)
                            printfn "Message_body: {%s}\n%s  @User%i  Retweet times: %i" (tweetInfo.Message_body) (tweetInfo.HashTag) (tweetInfo.Mention) (tweetInfo.No_of_retweets)
                            printfn "TID: %s" (tweetInfo.TId)

                    | "ReadSub" ->
                        query <- false
                        if not Simulation_flag then
                            let subReplyInfo = (Json.deserialize<seconday_reply_mssg> message)
                            
                            printfn "\n------------------------------------"
                            printfn "Name: %s" ("User" + subReplyInfo.Receiver_Id.ToString())
                            printf "Follow To: "
                            for id in subReplyInfo.Follower do
                                printf "User%i " id
                            printf "\nPublish To: "
                            for id in subReplyInfo.Source do
                                printf "User%i " id
                            printfn "\n"
                            printfn "[%s] Follow done" actorName
                            

                    | _ ->
                        printfn "[%s] Error in message" actorName

            | "PersonMode" ->
                let currentId = mssg_in?CurUserID.AsInteger()
                actorId <- currentId
                actorName <- "User" + currentId.ToString()

            | _ ->
                printfn "Client node \"%s\" received unknown message \"%s\"" actorName reqModel
                Environment.Exit 1
         
        return! loop()
    }
    loop()

let encryptPassword userid curr_password =
    let stringHash: string = System.Text.Encoding.ASCII.GetBytes $"{curr_password}" |> (new SHA256Managed()).ComputeHash |> Array.map (fun (x : byte) -> System.String.Format("{0:X2}", x)) |> String.concat System.String.Empty
    passmanager.Add(userid, curr_password)
    printfn "We will encrypt your password before we store it to our database so you can be assured of the safety!"
    let passJSON:request_mssg = { 
        Req = "password" ; 
        personId =  userid ;
        Name = "" ; 
        Key = Some (curr_password)
    }
    Json.serialize passJSON

let getInput (option:string) = 
    let mutable save_flag = true
    let mutable input_mssg = ""
    match option with
    | "int" ->
        while save_flag do
            printf "(Number): "
            input_mssg <- Console.ReadLine()
            match (Int32.TryParse(input_mssg)) with
            | (true, _) -> (save_flag <- false)
            | (false, _) ->  printfn "Invalid number"
        input_mssg
    | "string" ->
        while save_flag do
            printf "(String): "
            input_mssg <- Console.ReadLine()
            match input_mssg with
            | "" | "\n" | "\r" | "\r\n" | "\0" -> printfn "Invalid string"
            | _ -> (save_flag <- false)
        input_mssg
    | "YesNo" ->
        while save_flag do
            printf "Enter yes or no: "
            input_mssg <- Console.ReadLine()
            match input_mssg.ToLower() with
            | "yes" | "y" -> 
                (save_flag <- false) 
                input_mssg<-"yes"
            | "no" | "n" ->
                (save_flag <- false) 
                input_mssg<-"no"
            | _ -> printfn "Invalid input"
        input_mssg
    | _ ->
        input_mssg                                
    
let getRegistrationInfo (publicKey:string) =
    
    printfn "Create your userId "
    let userid = (int) (getInput "int")
    printfn "Create a password "
    let curr_password = (getInput "string")
    encryptPassword userid curr_password |> ignore
    printfn "Enter your name "
    let name = (getInput "string")
    let regJSON:request_mssg = { 
        Req = "RegisterUser" ; 
        personId =  userid ;
        Name = name ; 
        Key = Some (publicKey) ;
    }
    Json.serialize regJSON

let getLinkUn (option:string, currentId:int) = 
    if option = "Link" then
        printfn "Enter your userId : "
        let userid = (int) (getInput "int")
        printfn "Enter your password"
        let entered_password = (getInput "string")
        printfn "..please wait, matching decrypted password from database.."
        if(passmanager.ContainsKey(userid)) then
            if(not(passmanager.Item(userid).Equals(entered_password))) then
                printfn "Wrong password entered!"
                Environment.Exit 1

        let connectReq:ConnectInfo = {
            Req = "Link" ;
            personId = userid ;
        }
        Json.serialize connectReq
    else
        let connectReq:ConnectInfo = {
            Req = "UnLink" ;
            personId = currentId ;
        }
        Json.serialize connectReq

let getTweetMssg currentId = 
    let mutable hashtags = ""
    let mutable mention = -1
    printfn "Write your tweet "
    let content_body = (getInput "string")
    printfn "Do you want to add hashtags?"
    if (getInput "YesNo") = "yes" then
        printfn "Type your hashtag (with #): "
        hashtags <- (getInput "string")
    printfn "Do you want to mention someone?"
    if (getInput "YesNo") = "yes" then
        printfn "Type the User Id you would like to mention (with @): "
        mention <- (int) (getInput "int")

    let (tweetJSON:tweet) = {
        Req = "SendMessage" ;
        personId  = currentId ;
        TId = "" ;
        Time = (DateTime.Now) ;
        Message_body = content_body ;
        HashTag = hashtags ;
        Mention = mention ;
        No_of_retweets = 0 ;
    }
    Json.serialize tweetJSON

let getFollower currentId = 
    printfn "Enter the User Id you wish to follow "
    let subUserID = (int) (getInput "int")
    let (subJSON:seconday_info) = {
        Req = "Follow" ;
        personId = currentId ;
        SourceId = subUserID;
    }
    Json.serialize subJSON

let getReTreq currentId = 
    printfn "Enter the TweetId you wish to retweet "
    let reTId = (getInput "string")
    let (retweetJSON:RetweetInfo) = {
        Req = "Retweet" ;
        personId  = currentId ;
        Receiver_Id =  -1 ;
        Retweet_Key = reTId ;
    }
    Json.serialize retweetJSON

let genQueryJSON (option:string) =
    match option with
    | "Tag" ->
        printfn "Please enter the \"HashTag\" you would like to query (with #): "
        let hashtags = getInput "string"
        let (queryTagJSON:QueryInfo) = {
            Req = "Tag" ;
            personId = -1 ;
            HashTag = hashtags ;
        }
        Json.serialize queryTagJSON
    | "History" | "Mention" | "Subscribe" ->
        printfn "Please enter a \"personId\" you would like to \"%s\":" option
        let userid = (int) (getInput "int")
        let (queryJSON:QueryInfo) = {
            Req = option ;
            personId = userid ;
            HashTag = "" ;
        }
        Json.serialize queryJSON
    | _ -> 
        printfn "Wrong Input!"
        Environment.Exit 1
        ""

let getUserID (jsonStr:string) = 
    let mssg_in = JsonValue.Parse(jsonStr)
    (mssg_in?personId.AsInteger())

let register (client: IActorRef) = 
    client <! """{"Req":"RegisterUser"}"""
let sendTweet (client: IActorRef) (hashtag: string) (mention: int)= 
    let (request: tweet) = {
        Req = "SendMessage";
        personId = (int) client.Path.Name;
        TId = "";
        Time = DateTime.Now;
        Message_body = "Tweeeeeet";
        HashTag = hashtag;
        Mention = mention;
        No_of_retweets = 0 ;
    }
    client <! (Json.serialize request)
let subscribe (client: IActorRef) (publisher: IActorRef) = 
    let (request: seconday_info) = {
        Req = "Follow";
        personId = (int) client.Path.Name;
        SourceId = (int) publisher.Path.Name;
    }
    client <! (Json.serialize request)
let retweet (client: IActorRef) (targetUserID: int)=
    let (request: RetweetInfo) = {
        Req = "Retweet";
        Retweet_Key = "";
        Receiver_Id = targetUserID;
        personId = (int) client.Path.Name;
    }
    client <! (Json.serialize request)
let link (client: IActorRef) = 
    let (request: ConnectInfo) = {
        Req = "Link";
        personId = client.Path.Name |> int;
    }
    client <! (Json.serialize request)
let unlink (client: IActorRef) = 
    let (request: ConnectInfo) = {
        Req = "UnLink";
        personId = client.Path.Name |> int;
    }
    client <! (Json.serialize request)

let qHistory (client: IActorRef) = 
    let (request: QueryInfo) = {
        Req = "History";
        personId = client.Path.Name |> int;
        HashTag = "";
    }
    client <! (Json.serialize request)
let qMention (client: IActorRef) (mentionedUserID: int) = 
    let (request: QueryInfo) = {
        Req = "History";
        HashTag = "";
        personId = mentionedUserID;
    }
    client <! (Json.serialize request)
let qTag (client: IActorRef) (hashtags: string)= 
    let (request: QueryInfo) = {
        Req = "Tag";
        HashTag = hashtags;
        personId = 0;
    }
    client <! (Json.serialize request)
let qSubscription (client: IActorRef) (id: int) = 
    let (request: QueryInfo) = {
            Req = "Subscribe";
            HashTag = "";
            personId = id;
        }
    client <! (Json.serialize request)

let createActorClients (clientNumber: int) = 
    [1 .. clientNumber]
    |> List.map (fun id -> spawn system ((string) id) (BossNode false))
    |> List.toArray

let sample_store (arr: 'T []) (num: int) = 
    if arr.Length = 0 then 
        List.empty
    else
        let rnd = System.Random()    
        Seq.initInfinite (fun _ -> rnd.Next (arr.Length)) 
        |> Seq.distinct
        |> Seq.take(num)
        |> Seq.map (fun i -> arr.[i]) 
        |> Seq.toList

let mix (rand: Random) (l) = 
    l |> Array.sortBy (fun _ -> rand.Next()) 

let subscriberName (totalClients: int)= 
    let constant = List.fold (fun acc i -> acc + (1.0/i)) 0.0 [1.0 .. (float) totalClients]
    let res =
        [1.0 .. (float) totalClients] 
        |> List.map (fun x -> (float) totalClients/(x*constant) |> Math.Round |> int)
        |> List.toArray
    mix (Random()) res             

let tryTag (hashtags: string []) = 
    let random = Random()
    let rand () = random.Next(hashtags.Length-1)
    hashtags.[rand()]

let linkedId (linkedMembers: bool []) =
    [1 .. linkedMembers.Length-1]
    |> List.filter (fun i -> linkedMembers.[i])
    |> List.toArray

let unlinkId (linkedMembers: bool []) =
    [1 .. linkedMembers.Length-1]
    |> List.filter (fun i -> not linkedMembers.[i])
    |> List.toArray


let outputStyle (printStr:string) =
    // printfn "\n----------------------------------"
    printfn "\n<<< %s >>>\n" printStr
    // printfn "----------------------------------\n"

let take_input option = 
    match option with
    | "loginStep" ->
        printfn "\nWELCOME TO TWITTER!"
        printfn "~~~~~~~~~~~~~~~~~~~~~"
        printfn "Please choose from the list of following options:"
        printfn "Press 1 to login"
        printfn "Press 2 to sign up"
        printfn "Press 3 to know more"
    | "postSignIn" ->
        printfn "\nYou are logged in\n"
        printfn "Please choose from the list of following options:"
        printfn "Press 1 to Tweet"
        printfn "Press 2 to Retweet"
        printfn "Press 3 to follow an user"
        printfn "Press 4 to log out"
        printfn "Press 5 to see your tweets"
        printfn "Press 6 to view tweets by hashtags"
        printfn "Press 7 to view tweets by metions"
    | _ ->
        ()

let timedOut _ =
    loginSuccess <- TimerOver


let responseWaitTime (timeout:float) =
    let timer = new Timers.Timer(timeout*10000000.0)
    loginSuccess <- Wait
    timer.Elapsed.Add(timedOut)
    timer.Start()
    printfn "Waiting for server's reply..."
    while loginSuccess = Wait do ()
    timer.Close()

[<EntryPoint>]
let main argv =
    try
        mainTimer.Start()
        let whichAction = argv.[0]
        
        let mutable totalClients = 100
        let mutable HeightOfCycle = 100
        let mutable totalRequest = 100000000
        let hashtags = [|"#xyz";"#123"; "#AlinDobra"; "#DOSP"; "#UFL"; "#Agrade"|]

        let mutable avgOnline = 60
        let mutable avgTweetgone = 50
        let mutable avgRetweets = 20
        let mutable avgqHis = 20
        let mutable avgqMention = 10
        let mutable avgqTag = 10

        let mutable totalOnline = totalClients * avgOnline / 100
        let mutable totalTweetgone = totalOnline * avgTweetgone / 100
        let mutable totalRetweets = totalOnline * avgRetweets / 100
        let mutable totalqHis = totalOnline * avgqHis / 100
        let mutable totalqMention = totalOnline * avgqMention / 100
        let mutable totalqTag = totalOnline * avgqTag / 100
    
        
        if whichAction = "client" then

            let termianlRef = spawn system "EndNode" (BossNode true)
            let mutable currentId = -1
            let mutable currentState= 0
            
            (take_input "loginStep")
            while true do
                while currentState = 0 do
                    let inputStr = Console.ReadLine()
                    match inputStr with
                        | "1" | "login" ->
                            let Input_req = getLinkUn ("Link", -1)
                            let temp = getUserID Input_req
                            termianlRef <! Input_req
                            printfn "Fetching account information from server...\n%A" Input_req
                            responseWaitTime (5.0)
                            if loginSuccess = Success then
                                outputStyle ("Logged in as User "+ temp.ToString())
                                termianlRef <! """{"Req":"PersonMode", "CurUserID":"""+"\""+ temp.ToString() + "\"}"
                                currentId <- temp
                                currentState <- 1
                                (take_input "postSignIn")
                            else if loginSuccess = Failure then
                                outputStyle ("User with id: " + temp.ToString() + " does not exist")
                                (take_input "loginStep")
                            else
                                outputStyle ("Error occured while trying to login for User " + temp.ToString() + "\n(Server no response, timeout occurs)")
                                (take_input "loginStep")
                        | "2" | "signup" ->
                            let Input_req = getRegistrationInfo "key"
                            let temp = getUserID Input_req
                            termianlRef <! Input_req
                            printfn "Sedning your details to server...\n%A" Input_req
                            responseWaitTime (5.0)
                            if loginSuccess = Success then
                                outputStyle ("Welcome User"+ temp.ToString()+" to twitter!")

                                termianlRef <! """{"Req":"PersonMode", "CurUserID":"""+"\""+ temp.ToString() + "\"}"
                                currentId <- temp
                                currentState <- 1
                                (take_input "postSignIn")
                            else if loginSuccess = Failure then
                                outputStyle ("Unable to register User " + temp.ToString())
                                (take_input "loginStep")
                            else
                                outputStyle ("Unable to register User " + temp.ToString() + "\n(No response from the server)")
                                (take_input "loginStep")

                        | "3" | "knowmore" ->
                            printfn "-----------------------------------------------"
                            printfn "Developed by Tushar and Sankalp at Gainesville!"
                            printfn "-----------------------------------------------\n"
                            Environment.Exit 1
                        | _ ->
                            (take_input "loginStep")

                while currentState = 1 do
                    let inputStr = Console.ReadLine()
                    match inputStr with
                        | "1"| "sendtweet" ->
                            termianlRef <! getTweetMssg currentId
                            (take_input "postSignIn")
                        | "2"| "retweet" -> 
                            termianlRef <! getReTreq currentId
                            (take_input "postSignIn")
                        | "3"| "subscribe" | "sub" -> 
                            termianlRef <! getFollower currentId
                            (take_input "postSignIn")
                        | "4" | "unlink" ->
                            termianlRef <! getLinkUn ("UnLink", currentId)
                            responseWaitTime (5.0)
                            if loginSuccess = Success then
                                outputStyle ("Logged out User "+ currentId.ToString())
                                currentId <- -1
                                currentState <- 0
                                (take_input "loginStep")
                            else
                                outputStyle ("Faild to log out User " + currentId.ToString() + "\n(No response from server")
                                (take_input "postSignIn")
                        | "5"| "history" -> 
                            termianlRef <! genQueryJSON "History"
                            (take_input "postSignIn")
                        | "6"| "hashtags" -> 
                            termianlRef <! genQueryJSON "Tag"
                            (take_input "postSignIn")
                        | "7"| "mention" | "men" -> 
                            termianlRef <! genQueryJSON "Mention"
                            (take_input "postSignIn")
                        | _ ->
                            (take_input "postSignIn")

        else if whichAction = "simulate" then
            printfn "\n\n~Simulation~\n"

            printf "Enter the no. of users you would like to have in your testing\n"
            totalClients <- getInput "int" |> int

            Simulation_flag <- true
        else
            printfn "\n\nUnsupported input type, you can run as 'simulate' or 'customer' only\n"
            Environment.Exit 1

        printfn "\n\n...please wait...\n"

        let clientsInfo = createActorClients totalClients

        let followerCount = subscriberName totalClients
        
        clientsInfo 
        |> Array.map(fun client -> 
            async{
                register client
            })
        |> Async.Parallel
        |> Async.RunSynchronously
        |> ignore
        
        System.Threading.Thread.Sleep (max totalClients 3000)

        //Following the zipf
        clientsInfo
        |> Array.mapi(fun i client ->
            async {
                let sub = followerCount.[i]
                let mutable s = Set.empty
                let rand = Random()
                while s.Count < sub do
                    let subscriber = rand.Next(totalClients-1)
                    if clientsInfo.[subscriber].Path.Name <> client.Path.Name && not (s.Contains(subscriber)) then
                        s <- s.Add(subscriber)
                        subscribe (clientsInfo.[subscriber]) client
            })
        |> Async.Parallel
        |> Async.RunSynchronously
        |> ignore
        
        clientsInfo
        |> Array.mapi (fun i client ->
            async{
                let randA = Random()
                let randB = Random(randA.Next())
                let totalTweets = max (randA.Next(1,5) * followerCount.[i]) 1
                
                for i in 1 .. totalTweets do
                    sendTweet client (tryTag hashtags) (randB.Next(totalClients))
            })
        |> Async.Parallel
        |> Async.RunSynchronously
        |> ignore

        clientsInfo
        |> Array.map (fun client -> 
            async{
                unlink client
            })
        |> Async.Parallel
        |> Async.RunSynchronously
        |> ignore

        System.Threading.Thread.Sleep (max totalClients 2000)
        printfn "\n\nSimulation setup completed"
          
    with | :? IndexOutOfRangeException ->
            printfn "\n\nUnsupported input type, you can run as 'simulate' or 'client' only\n\n"

         | :? FormatException ->
            printfn "\nFormat Exception occured!\n"
    0