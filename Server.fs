open System
open System.Diagnostics
open System.Security.Cryptography
open System.Globalization
open System.Collections.Generic
open System.Text
open Akka.Actor
open Akka.FSharp

open FSharp.Data
open FSharp.Data.JsonExtensions
open FSharp.Json

type QueryActorMsg =
   | History of string * IActorRef * string[]
   | Tag of string * IActorRef * string[]
   | Mention of string * IActorRef * string[]

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

let registrationMap = new Dictionary<int, request_mssg>()
let tInfoMap = new Dictionary<string, tweet>()
let histInfoMap = new Dictionary<int, List<string>>()
let tagInfoMap = new Dictionary<string, List<string>>()
let publisherInfoMap = new Dictionary<int, List<int>>()
let subscriberInfoMap = new Dictionary<int, List<int>>()
let menInfoMap = new Dictionary<int, List<string>>()

let config =
    Configuration.parse
        @"akka {
            log-config-on-start = off
            log-dead-letters = off
            log-dead-letters-during-shutdown = off
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""

            }

            remote {
                log-received-messages = off
                log-sent-messages = off
                helios.tcp {
                    hostname = localhost
                    port = 9001
                }
            }
        }"

let system = System.create "TwitterEngine" config
let totalQWorker = 1000
let mutable displayStatMode = 0
let gloTimr = Stopwatch()
let actorCreator f = actorOf f

let checkUserValidity usr_id = 
    (registrationMap.ContainsKey(usr_id)) 

let registrationInfoAddr (usr_id, info:request_mssg) =
    registrationMap.Add(usr_id, info)
let registrationInfoAddrActorRef =
    actorCreator registrationInfoAddr |> spawn system "registrationMap-Adder"

let updRegistrationMap (twInfo:request_mssg) =
    let usr_id = twInfo.personId
    if not (registrationMap.ContainsKey(usr_id)) then
        registrationInfoAddrActorRef <! (usr_id, twInfo)
        "Success"
    else
        "Fail"

let updHistMap usr_id tweet_id =
    if usr_id >= 0 && (checkUserValidity usr_id) then        
        if not (histInfoMap.ContainsKey(usr_id)) then
            let randList = new List<string>()
            randList.Add(tweet_id)
            histInfoMap.Add(usr_id, randList)
        else
            if not (histInfoMap.[usr_id].Contains(tweet_id)) then
                (histInfoMap.[usr_id]).Add(tweet_id)
    
let updTagMap tag tweet_id = 
    if tag <> "" && tag.[0] = '#' then
        if not (tagInfoMap.ContainsKey(tag)) then
            let randList = new List<string>()
            randList.Add(tweet_id)
            tagInfoMap.Add(tag, randList)
        else
            (tagInfoMap.[tag]).Add(tweet_id)

let updPubSubMap pub_id sub_id = 
    let mutable failed_flag = false
    if pub_id <> sub_id && (checkUserValidity pub_id) && (checkUserValidity sub_id) then
        if not (publisherInfoMap.ContainsKey(pub_id)) then
            let randList = new List<int>()
            randList.Add(sub_id)
            publisherInfoMap.Add(pub_id, randList)
        else
            if not ((publisherInfoMap.[pub_id]).Contains(sub_id)) then
                (publisherInfoMap.[pub_id]).Add(sub_id)
            else
                failed_flag <- true

        if not (subscriberInfoMap.ContainsKey(sub_id)) then
            let randList = new List<int>()
            randList.Add(pub_id)
            subscriberInfoMap.Add(sub_id, randList)
        else
            if not ((subscriberInfoMap.[sub_id]).Contains(pub_id)) then
                (subscriberInfoMap.[sub_id]).Add(pub_id)
            else
                failed_flag <- true
        if failed_flag then
            "Fail"
        else
            "Success"
    else
        "Fail"

let updMentionMap usr_id tweet_id =
    if usr_id >= 0 && (checkUserValidity usr_id) then
       if not (menInfoMap.ContainsKey(usr_id)) then
            let randList = new List<string>()
            randList.Add(tweet_id)
            menInfoMap.Add(usr_id, randList)
        else
            (menInfoMap.[usr_id]).Add(tweet_id)

let updTweetMap (twInfo:tweet) =
    let tweet_id = twInfo.TId
    let usr_id = twInfo.personId
    let tag = twInfo.HashTag
    let mention = twInfo.Mention
    tInfoMap.Add(tweet_id, twInfo)
    updHistMap usr_id tweet_id
    updTagMap tag tweet_id
    updMentionMap mention tweet_id
    updHistMap mention tweet_id

    if (publisherInfoMap.ContainsKey(usr_id)) then
        for sub_id in (publisherInfoMap.[usr_id]) do
            updHistMap sub_id tweet_id

let updRetweetInfo usr_id (orginalTwInfo:tweet) =
    let nwTwInfo:tweet = {
        Req = orginalTwInfo.Req ;
        personId  = orginalTwInfo.personId ;
        TId = orginalTwInfo.TId ;
        Time = orginalTwInfo.Time ;
        Message_body = orginalTwInfo.Message_body ;
        HashTag = orginalTwInfo.HashTag ;
        Mention = orginalTwInfo.Mention ;
        No_of_retweets = (orginalTwInfo.No_of_retweets+1) ;
    }
    tInfoMap.[orginalTwInfo.TId] <- nwTwInfo
    updHistMap usr_id (orginalTwInfo.TId)
   
    if (publisherInfoMap.ContainsKey(usr_id)) then
        for sub_id in (publisherInfoMap.[usr_id]) do
            updHistMap sub_id (orginalTwInfo.TId)         

let assgnTweetIDInfo (orginalTwInfo:tweet) =
    let nwTwInfo:tweet = {
        Req = orginalTwInfo.Req;
        personId  = orginalTwInfo.personId;
        TId = (tInfoMap.Count + 1).ToString();
        Time = orginalTwInfo.Time;
        Message_body = orginalTwInfo.Message_body;
        HashTag = orginalTwInfo.HashTag;
        Mention = orginalTwInfo.Mention;
        No_of_retweets = orginalTwInfo.No_of_retweets;
    }
    nwTwInfo

let tweteQActor (mailbox:Actor<QueryActorMsg>) =
    let nodeName = "QueryActor " + mailbox.Self.Path.Name
    let rec loop() = actor {
        let! (message: QueryActorMsg) = mailbox.Receive()
       
        match message with
            | History (json,sender, tweet_arr) ->
                let  messge = JsonValue.Parse(json)
                let  usr_id = messge?personId.AsInteger()
                let mutable tweetCnt = 0
                for tweet_id in (tweet_arr) do
                    if tInfoMap.ContainsKey(tweet_id) then
                        tweetCnt <- tweetCnt + 1
                        let ShowTweetReply:reply_mssg = {
                            Req = "Replymssg" ;
                            Type = "ReadTweet" ;
                            Report = tweetCnt ;
                            tweet = tInfoMap.[tweet_id] ;
                        }
                        sender <! (Json.serialize ShowTweetReply)
                let (ShowTweetReply:reply) = { 
                    Req = "Replymssg" ;
                    Type = "History" ;
                    Report =  "Success" ;
                    Description =  Some "Query history Tweets done" ;
                }
                sender <! (Json.serialize ShowTweetReply)  

            | Tag (json, sender, tweetIDarray) ->
                let  messge = JsonValue.Parse(json)
                let tag = messge?HashTag.AsString()
                let mutable tweetCnt = 0
                for tweet_id in tweetIDarray do
                    if tInfoMap.ContainsKey(tweet_id) then
                        tweetCnt <- tweetCnt + 1
                        
                        let ShowTweetReply:reply_mssg = {
                            Req = "Replymssg" ;
                            Type = "ReadTweet" ;
                            Report = tweetCnt ;
                            tweet = tInfoMap.[tweet_id] ;
                        }
                        sender <! (Json.serialize ShowTweetReply)

                let (reply:reply) = { 
                    Req = "Replymssg" ;
                    Type = "History" ;
                    Report =  "Success" ;
                    Description =  Some ("Query Tweets with "+tag+ " done") ;
                }
                sender <! (Json.serialize reply)  

            | Mention (json, sender,tweetIDarray) ->
                let  messge = JsonValue.Parse(json)
                let  usr_id = messge?personId.AsInteger()
                let  request_type = messge?Req.AsString()
                let mutable tweetCnt = 0
                for tweet_id in (tweetIDarray) do
                    if tInfoMap.ContainsKey(tweet_id) then
                        tweetCnt <- tweetCnt + 1
                        let ShowTweetReply:reply_mssg = {
                            Req = "Replymssg" ;
                            Type = "ReadTweet" ;
                            Report = tweetCnt ;
                            tweet = tInfoMap.[tweet_id] ;
                        }
                        sender <! (Json.serialize ShowTweetReply)

                let (QueryHistoryReply:reply) = { 
                    Req = "Replymssg" ;
                    Type = "History" ;
                    Report =  "Success" ;
                    Description =  Some "Query mentioned Tweets done" ;
                }
                sender <! (Json.serialize QueryHistoryReply)
        return! loop()
    }
    loop()

let startQActors (clientNum: int) = 
    [1 .. clientNum]
    |> List.map (fun id -> spawn system ("Q"+id.ToString()) tweteQActor)
    |> List.toArray
let qWorker = startQActors totalQWorker
let srvrActNode (serverMailbox:Actor<string>) =
    let nodeName = serverMailbox.Self.Path.Name
    
    let mutable processed_msg = 0
    let mutable online_usr = Set.empty
    let updOnlineUsrMap usr_id opt = 
        let is_connected_flag = online_usr.Contains(usr_id)
        if opt = "connect" && not is_connected_flag then
            if checkUserValidity usr_id then
                online_usr <- online_usr.Add(usr_id)
                0
            else
                -1
        else if opt = "disconnect" && is_connected_flag then
            online_usr <- online_usr.Remove(usr_id)
            0
        else
            0
    let mutable totalRequests = 0
    let mutable maxThrougput = 0
    
    let showServerStatus _ =
        totalRequests <- totalRequests + processed_msg
        maxThrougput <- Math.Max(maxThrougput, processed_msg)
        if displayStatMode = 0 then    
            printfn "\n ******** Status of Server ********"
            printfn "Requests processed: %i" totalRequests
            printfn "Tweets in DataBase: %i" (tInfoMap.Keys.Count)
            printfn "Registered Users: %i" (registrationMap.Keys.Count)
            printfn "Online Users: %i" (online_usr.Count)
            printfn "***********************************\n"
        processed_msg <- 0

    let timer = new Timers.Timer(1000.0)
    timer.Elapsed.Add(showServerStatus)
    timer.Start()

    let rec loop() = actor {
        let! (message: string) = serverMailbox.Receive()
        let  sender = serverMailbox.Sender()
        let  messge = JsonValue.Parse(message)
        let  request_type = messge?Req.AsString()
        let  usr_id = messge?personId.AsInteger()
        match request_type with
            | "RegisterUser" ->
                let regMsg = (Json.deserialize<request_mssg> message)
                
                let status = updRegistrationMap regMsg
                let reply:reply = { 
                    Req = "Replymssg" ;
                    Type = request_type ;
                    Report =  status ;
                    Description =  Some (regMsg.personId.ToString()) ;
                }
                
                sender <!  (Json.serialize reply)

            | "SendMessage" ->
                let originalTwtInfo = (Json.deserialize<tweet> message)
                let twtInfo = assgnTweetIDInfo originalTwtInfo
                if (checkUserValidity twtInfo.personId) then
                    updTweetMap twtInfo

                    let (reply:reply) = { 
                        Req = "Replymssg" ;
                        Type = request_type ;
                        Report =  "Success" ;
                        Description =  Some "Tweet sent successfully from server" ;
                    }
                    sender <! (Json.serialize reply)
                else
                    let (reply:reply) = { 
                        Req = "Replymssg" ;
                        Type = request_type ;
                        Report =  "Failed" ;
                        Description =  Some "Please make sure that the user is signed up before making a tweet! " ;
                    }
                    sender <! (Json.serialize reply)

            | "Retweet" ->
                let retweet_id = messge?Retweet_Key.AsString()
                let tUserID = messge?Receiver_Id.AsInteger()
                let mutable failed_flag = false
                if retweet_id = "" then
                    if (checkUserValidity tUserID) && histInfoMap.ContainsKey(tUserID) && histInfoMap.[tUserID].Count > 0 then
                        let rand = Random()
                        let numTweet = histInfoMap.[tUserID].Count
                        let randIdx = rand.Next(numTweet)
                        let tgtRetweet_id = histInfoMap.[tUserID].[randIdx]
                        let retweetInfo = tInfoMap.[tgtRetweet_id]
                        printfn "retweetInfo.personId = %i" retweetInfo.personId
                        printfn "userID = %i" usr_id
                        if (retweetInfo.personId <> usr_id) then
                            updRetweetInfo usr_id retweetInfo
                        else
                            failed_flag <- true
                    else
                        failed_flag <- true
                else
                    if tInfoMap.ContainsKey(retweet_id) then
                        if (tInfoMap.[retweet_id].personId) <> usr_id then
                            updRetweetInfo usr_id (tInfoMap.[retweet_id])
                        else
                            failed_flag <- true
                    else
                        failed_flag <- true
                if failed_flag then
                    let (reply:reply) = { 
                        Req = "Replymssg" ;
                        Type = "SendMessage" ;
                        Report =  "Failed" ;
                        Description =  Some "Unable to choose a random tweet" ;
                    }
                    sender <! (Json.serialize reply)
                else
                    let (reply:reply) = { 
                        Req = "Replymssg" ;
                        Type = "SendMessage" ;
                        Report =  "Success" ;
                        Description =  Some "Retweet successful!" ;
                    }
                    sender <! (Json.serialize reply)
            | "Follow" ->
                let status = updPubSubMap (messge?SourceId.AsInteger()) (messge?personId.AsInteger())
                let (reply:reply) = { 
                        Req = "Replymssg" ;
                        Type = request_type ;
                        Report =  status ;
                        Description =  None ;
                }
                sender <! (Json.serialize reply)

            | "Link" ->
                let usr_id = messge?personId.AsInteger()
                let returnInfo = (updOnlineUsrMap usr_id "connect")
                if returnInfo < 0 then
                    let (reply:reply) = { 
                        Req = "Replymssg" ;
                        Type = request_type ;
                        Report =  "Fail" ;
                        Description =  Some "Please sign up first" ;
                    }
                    sender <! (Json.serialize reply)
                else 
                    let (reply:reply) = { 
                        Req = "Replymssg" ;
                        Type = request_type ;
                        Report =  "Success" ;
                        Description =  Some (usr_id.ToString()) ;
                    }
                    sender <! (Json.serialize reply)
                
            | "UnLink" ->
                
                (updOnlineUsrMap usr_id "disconnect") |> ignore
                let (reply:reply) = { 
                    Req = "Replymssg" ;
                    Type = request_type ;
                    Report =  "Success" ;
                    Description =   Some (usr_id.ToString()) ;
                }
                sender <! (Json.serialize reply)

            | "History" ->
                    
                if not (histInfoMap.ContainsKey(usr_id)) then
                    let (reply:reply) = { 
                        Req = "Replymssg" ;
                        Type = request_type ;
                        Report =  "NoTweet" ;
                        Description =  Some "No new tweets at the moment" ;
                    }
                    sender <! (Json.serialize reply)
                else
                    let rand = Random()
                    let randIdx = rand.Next(totalQWorker)
                    qWorker.[randIdx] <! History (message, sender, histInfoMap.[usr_id].ToArray())    

            | "Mention" ->
                if not (menInfoMap.ContainsKey(usr_id)) then
                    let (reply:reply) = { 
                        Req = "Replymssg" ;
                        Type = "History" ;
                        Report =  "NoTweet" ;
                        Description =  Some "Finished querying,no mentioned tweet to show" ;
                    }
                    sender <! (Json.serialize reply)
                else
                    let rand = Random()
                    let randIdx = rand.Next(totalQWorker)
                    qWorker.[randIdx] <! Mention (message, sender, menInfoMap.[usr_id].ToArray())  

            | "Tag" ->
                let tag = messge?HashTag.AsString()
                if not (tagInfoMap.ContainsKey(tag)) then
                    let (reply:reply) = { 
                        Req = "Replymssg" ;
                        Type = "History" ;
                        Report =  "NoTweet" ;
                        Description =  Some ("Finished querying, no tweets with tag " + tag)  ;
                    }
                    sender <! (Json.serialize reply)
                else
                    let rand = Random()
                    let randIdx = rand.Next(totalQWorker)
                    qWorker.[randIdx] <! Tag (message, sender, tagInfoMap.[tag].ToArray())    

            | "Subscribe" ->
                if not (subscriberInfoMap.ContainsKey(usr_id)) && not (publisherInfoMap.ContainsKey(usr_id))then
                    let (reply:reply) = { 
                        Req = "Replymssg" ;
                        Type = "History" ;
                        Report =  "NoTweet" ;
                        Description =  Some ("Query done, the user has no any subscribers or subscribes to others ")  ;
                    }
                    sender <! (Json.serialize reply)
                else if (subscriberInfoMap.ContainsKey(usr_id)) && not (publisherInfoMap.ContainsKey(usr_id))then
                    let subscription_reply_info:seconday_reply_mssg = {
                        Req = "Replymssg" ;
                        Type = "ShowSub" ;
                        Receiver_Id = usr_id ;
                        Follower = subscriberInfoMap.[usr_id].ToArray() ;
                        Source = [||] ;
                    }
                    sender <! (Json.serialize subscription_reply_info)    
                else if not (subscriberInfoMap.ContainsKey(usr_id)) && (publisherInfoMap.ContainsKey(usr_id))then
                    let subscription_reply_info:seconday_reply_mssg = {
                        Req = "Replymssg" ;
                        Type = "ShowSub" ;
                        Receiver_Id = usr_id ;
                        Follower = [||] ;
                        Source = publisherInfoMap.[usr_id].ToArray() ;
                    }
                    sender <! (Json.serialize subscription_reply_info)
                else 
                    let subscription_reply_info:seconday_reply_mssg = {
                        Req = "Replymssg" ;
                        Type = "ShowSub" ;
                        Receiver_Id = usr_id ;
                        Follower = subscriberInfoMap.[usr_id].ToArray() ;
                        Source = publisherInfoMap.[usr_id].ToArray() ;
                    }
                    sender <! (Json.serialize subscription_reply_info)                    

            | _ ->
                printfn "customer \"%s\" got some unknown messge \"%s\"" nodeName request_type
                Environment.Exit 1

        processed_msg <- processed_msg + 1
        return! loop()
    }
    loop()

let mutable a = 1
[<EntryPoint>]
let main argv =
    try
        let serverActor = spawn system "TWServer" srvrActNode
        gloTimr.Start()
        printfn "\n\n************************************"
        printfn "Enter cmd \"exit or quit\" to exit the program."
        printfn "****************************************\n"
        while true do
            let inp_str = Console.ReadLine()
            match inp_str with
            | "exit" | "quit" ->
                gloTimr.Stop()
                printfn "\n\nTwitter Exited...\n Running time: %A\n" (gloTimr.Elapsed)
                Environment.Exit 1

    with | :? IndexOutOfRangeException ->
            printfn "\n Not correct Input!\n"

         | :?  FormatException ->
            printfn "\nWrong format!\n"
    0 
