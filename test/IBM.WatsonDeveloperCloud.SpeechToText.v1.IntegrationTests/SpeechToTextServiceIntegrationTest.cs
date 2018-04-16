﻿/**
* Copyright 2017 IBM Corp. All Rights Reserved.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
*      http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*
*/

using IBM.WatsonDeveloperCloud.SpeechToText.v1.Model;
using IBM.WatsonDeveloperCloud.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace IBM.WatsonDeveloperCloud.SpeechToText.v1.IntegrationTests
{
    [TestClass]
    public class SpeechToTextServiceIntegrationTest
    {
        //private const string SESSION_STATUS_INITIALIZED = "initialized";
        private static string _username;
        private static string _password;
        private static string _endpoint;
        private AutoResetEvent autoEvent = new AutoResetEvent(false);
        private static string credentials = string.Empty;
        private const string EN_US = "en-US_BroadbandModel";
        private string _customModelName = "dotnet-integration-test-custom-model";
        private string _customModelDescription = "A custom model to test .NET SDK Speech to Text customization.";
        private string _corpusName = "The Jabberwocky";
        private string _corpusPath = @"SpeechToTextTestData/theJabberwocky-utf8.txt";
        private string _acousticModelName = "dotnet-integration-test-custom-acoustic-model";
        private string _acousticModelDescription = "A custom model to teset .NET SDK Speech to Text acoustic customization.";
        private string _acousticResourceUrl = "https://ia802302.us.archive.org/10/items/Greatest_Speeches_of_the_20th_Century/TheFirstAmericaninEarthOrbit.mp3";
        private string _acousticResourceName = "firstOrbit";
        private string _acousticResourceMimeType = "audio/mpeg";
        private SpeechToTextService _service;

        [TestInitialize]
        public void Setup()
        {

            if (string.IsNullOrEmpty(credentials))
            {
                try
                {
                    credentials = Utility.SimpleGet(
                        Environment.GetEnvironmentVariable("VCAP_URL"),
                        Environment.GetEnvironmentVariable("VCAP_USERNAME"),
                        Environment.GetEnvironmentVariable("VCAP_PASSWORD")).Result;
                }
                catch (Exception e)
                {
                    Console.WriteLine(string.Format("Failed to get credentials: {0}", e.Message));
                }

                Task.WaitAll();

                var vcapServices = JObject.Parse(credentials);
                _endpoint = vcapServices["speech_to_text"]["url"].Value<string>();
                _username = vcapServices["speech_to_text"]["username"].Value<string>();
                _password = vcapServices["speech_to_text"]["password"].Value<string>();
            }

            _service = new SpeechToTextService(_username, _password);
            _service.Endpoint = _endpoint;
        }

        #region Models
        [TestMethod]
        public void TestModels_Success()
        {
            var listModelsResult = ListModels();

            var getModelResult = GetModel(EN_US);

            Assert.IsNotNull(getModelResult);
            Assert.IsTrue(getModelResult.Name == EN_US);
            Assert.IsNotNull(listModelsResult);
            Assert.IsNotNull(listModelsResult.Models);
        }
        #endregion

        #region Custom Language Models
        [TestMethod]
        public void TestCustomLanguageModels_Success()
        {
            var listLanguageModelsResult = ListLanguageModels();

            CreateLanguageModel createLanguageModel = new Model.CreateLanguageModel
            {
                Name = _customModelName,
                BaseModelName = EN_US,
                Description = _customModelDescription
            };

            var createLanguageModelResult = CreateLanguageModel(createLanguageModel);
            string customizationId = createLanguageModelResult.CustomizationId;

            var getLanguageModelResult = GetLanguageModel(customizationId);

            var listCorporaResults = ListCorpora(customizationId);

            object addCorpusResults = null;
            using (FileStream corpusStream = File.OpenRead(_corpusPath))
            {
                addCorpusResults = AddCorpus(customizationId, _corpusName, corpusStream);
            }

            var getCorpusResults = GetCorpus(customizationId, _corpusName);

            CheckCorpusStatus(customizationId, _corpusName);
            autoEvent.WaitOne();

            var trainLanguageModelResult = TrainLanguageModel(customizationId);
            CheckCustomizationStatus(customizationId);
            autoEvent.WaitOne();
            Assert.IsNotNull(trainLanguageModelResult);
            trainLanguageModelResult = null;

            var listCustomWordsResult = ListWords(customizationId);

            var customWords = new CustomWords()
            {
                Words = new List<CustomWord>()
                            {
                                new CustomWord()
                                {
                                    DisplayAs = "Watson",
                                    SoundsLike = new List<string>()
                                    {
                                        "wat son"
                                    },
                                    Word = "watson"
                                },
                                new CustomWord()
                                {
                                    DisplayAs = "C#",
                                    SoundsLike = new List<string>()
                                    {
                                        "si sharp"
                                    },
                                    Word = "csharp"
                                },
                                new CustomWord()
                                {
                                    DisplayAs = "SDK",
                                    SoundsLike = new List<string>()
                                    {
                                        "S.D.K."
                                    },
                                    Word = "sdk"
                                }
                            }
            };

            var addCustomWordsResult = AddWords(customizationId, customWords);

            CheckCustomizationStatus(customizationId);
            autoEvent.WaitOne();

            trainLanguageModelResult = TrainLanguageModel(customizationId);
            Assert.IsNotNull(trainLanguageModelResult);
            trainLanguageModelResult = null;

            CheckCustomizationStatus(customizationId);
            autoEvent.WaitOne();

            var customWord = new CustomWord()
            {
                DisplayAs = ".NET",
                SoundsLike = new List<string>()
                {
                    "dotnet"
                },
                Word = "dotnet"
            };

            var addCustomWordResult = AddWord(customizationId, "dotnet", customWord);

            var getCustomWordResult = GetWord(customizationId, "dotnet");

            trainLanguageModelResult = TrainLanguageModel(customizationId);
            CheckCustomizationStatus(customizationId);
            autoEvent.WaitOne();
            Assert.IsNotNull(trainLanguageModelResult);
            trainLanguageModelResult = null;

            CheckCorpusStatus(customizationId, _corpusName);
            autoEvent.WaitOne();

            //var upgradeLanguageModelResult = UpgradeLanguageModel(customizationId);
            //  Assert.IsNotNull(upgradeLanguageModelResult);

            var deleteCustomWordResults = DeleteWord(customizationId, "csharp");

            var deleteCorpusResults = DeleteCorpus(customizationId, _corpusName);

            var resetLanguageModelResult = ResetLanguageModel(customizationId);
            Assert.IsNotNull(resetLanguageModelResult);

            var deleteLanguageModelResults = DeleteLanguageModel(customizationId);

            Assert.IsNotNull(deleteCustomWordResults);
            Assert.IsNotNull(deleteCorpusResults);
            Assert.IsNotNull(deleteLanguageModelResults);
            Assert.IsNotNull(getCustomWordResult);
            Assert.IsTrue(getCustomWordResult._Word == "dotnet");
            Assert.IsNotNull(addCustomWordResult);
            Assert.IsNotNull(addCustomWordsResult);
            Assert.IsNotNull(listCustomWordsResult);
            Assert.IsNotNull(listCustomWordsResult._Words);
            Assert.IsNotNull(getCorpusResults);
            Assert.IsTrue(getCorpusResults.Name == _corpusName);
            Assert.IsNotNull(addCorpusResults);
            Assert.IsNotNull(listCorporaResults);
            Assert.IsNotNull(listCorporaResults._Corpora);
            Assert.IsNotNull(getLanguageModelResult);
            Assert.IsTrue(getLanguageModelResult.CustomizationId == customizationId);
            Assert.IsNotNull(createLanguageModelResult);
            Assert.IsNotNull(listLanguageModelsResult);
            Assert.IsNotNull(listLanguageModelsResult.Customizations);
        }
        #endregion

        #region Acoustic Customizations
        [TestMethod]
        public void TestAcousticCustomizations()
        {
            byte[] acousticResourceData = null;

            try
            {
                acousticResourceData = DownloadAcousticResource(_acousticResourceUrl).Result;
            }
            catch (Exception e)
            {
                Console.WriteLine(string.Format("Failed to get acoustic data: {0}", e.Message));
            }

            Task.WaitAll();

            var listAcousticModelsResult = ListAcousticModels();

            var acousticModel = new CreateAcousticModel
            {
                Name = _acousticModelName,
                BaseModelName = EN_US,
                Description = _acousticModelDescription
            };

            var createAcousticModelResult = CreateAcousticModel(acousticModel);
            var acousticCustomizationId = createAcousticModelResult.CustomizationId;

            var getAcousticModelResult = GetAcousticModel(acousticCustomizationId);


            var listAudioResult = ListAudio(acousticCustomizationId);

            object addAudioResult = null;

            addAudioResult = AddAudio(acousticCustomizationId, _acousticResourceName, acousticResourceData, _acousticResourceMimeType, allowOverwrite: true);

            var getAudioResult = GetAudio(acousticCustomizationId, _acousticResourceName);

            CheckAudioStatus(acousticCustomizationId, _acousticResourceName);
            autoEvent.WaitOne();

            CheckAcousticCustomizationStatus(acousticCustomizationId);
            autoEvent.WaitOne();

            var trainAcousticModelResult = TrainAcousticModel(acousticCustomizationId);

            CheckAcousticCustomizationStatus(acousticCustomizationId);
            autoEvent.WaitOne();

            //var upgradeAcousticModel = UpgradeAcousticModel(acousticCustomizationId);

            //CheckAcousticCustomizationStatus(acousticCustomizationId);
            //autoEvent.WaitOne();

            var deleteAudioResult = DeleteAudio(acousticCustomizationId, _acousticResourceName);

            var resetAcousticModelResult = ResetAcousticModel(acousticCustomizationId);

            var deleteAcousticModelResult = DeleteAcousticModel(acousticCustomizationId);

            Assert.IsNotNull(deleteAudioResult);
            Assert.IsNotNull(deleteAcousticModelResult);

            Assert.IsNotNull(getAudioResult);
            Assert.IsTrue(getAudioResult.Name == _acousticResourceName);
            Assert.IsNotNull(addAudioResult);
            Assert.IsNotNull(listAudioResult);
            Assert.IsNotNull(listAudioResult.Audio);
            Assert.IsNotNull(getAcousticModelResult);
            Assert.IsTrue(getAcousticModelResult.Name == _acousticModelName);
            Assert.IsTrue(getAcousticModelResult.Description == _acousticModelDescription);
            Assert.IsNotNull(createAcousticModelResult);
            Assert.IsNotNull(listAcousticModelsResult);
        }
        #endregion

        #region Sessions
        [TestMethod]
        public void TestSessions_Success()
        {
            byte[] acousticResourceData = null;

            try
            {
                acousticResourceData = DownloadAcousticResource(_acousticResourceUrl).Result;
            }
            catch (Exception e)
            {
                Console.WriteLine(string.Format("Failed to get acoustic data: {0}", e.Message));
            }

            Task.WaitAll();

            Stream audioStream = new MemoryStream(acousticResourceData);

            string sessionModel = "en-US_BroadbandModel";
            var createSessionResult = _service.CreateSession(sessionModel);

            var getSessionStatusResult = _service.GetSessionStatus(createSessionResult.SessionId);

            var recognizeAudioResult = _service.RecognizeWithSession(createSessionResult.SessionId, "audio/mp3", audioStream);

            var observeResult = _service.ObserveResult(createSessionResult.SessionId);

            var deleteSesionResult = _service.DeleteSession(createSessionResult.SessionId);

            Assert.IsNotNull(deleteSesionResult);
            Assert.IsNotNull(observeResult);
            Assert.IsNotNull(recognizeAudioResult);
            Assert.IsNotNull(createSessionResult);
            Assert.IsTrue(createSessionResult.Model == sessionModel);
            Assert.IsNotNull(getSessionStatusResult);
            Assert.IsTrue(getSessionStatusResult.Session.SessionId == createSessionResult.SessionId);
        }
        #endregion

        #region Generated
        #region GetModel
        private SpeechModel GetModel(string modelId)
        {
            Console.WriteLine("\nAttempting to GetModel()");
            var result = _service.GetModel(modelId: modelId);

            if (result != null)
            {
                Console.WriteLine("GetModel() succeeded:\n{0}", JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            else
            {
                Console.WriteLine("Failed to GetModel()");
            }

            return result;
        }
        #endregion

        #region ListModels
        private SpeechModels ListModels()
        {
            Console.WriteLine("\nAttempting to ListModels()");
            var result = _service.ListModels();

            if (result != null)
            {
                Console.WriteLine("ListModels() succeeded:\n{0}", JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            else
            {
                Console.WriteLine("Failed to ListModels()");
            }

            return result;
        }
        #endregion

        #region RecognizeSessionless
        private SpeechRecognitionResults RecognizeSessionless(string model = null, string customizationId = null, string acousticCustomizationId = null, string baseModelVersion = null, double? customizationWeight = null, byte[] audio = null, string contentType = null, long? inactivityTimeout = null, List<string> keywords = null, float? keywordsThreshold = null, long? maxAlternatives = null, float? wordAlternativesThreshold = null, bool? wordConfidence = null, bool? timestamps = null, bool? profanityFilter = null, bool? smartFormatting = null, bool? speakerLabels = null)
        {
            Console.WriteLine("\nAttempting to RecognizeSessionless()");
            var result = _service.RecognizeSessionless(model: model, customizationId: customizationId, acousticCustomizationId: acousticCustomizationId, baseModelVersion: baseModelVersion, customizationWeight: customizationWeight, audio: audio, contentType: contentType, inactivityTimeout: inactivityTimeout, keywords: keywords, keywordsThreshold: keywordsThreshold, maxAlternatives: maxAlternatives, wordAlternativesThreshold: wordAlternativesThreshold, wordConfidence: wordConfidence, timestamps: timestamps, profanityFilter: profanityFilter, smartFormatting: smartFormatting, speakerLabels: speakerLabels);

            if (result != null)
            {
                Console.WriteLine("RecognizeSessionless() succeeded:\n{0}", JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            else
            {
                Console.WriteLine("Failed to RecognizeSessionless()");
            }

            return result;
        }
        #endregion

        #region CheckJob
        private RecognitionJob CheckJob(string id)
        {
            Console.WriteLine("\nAttempting to CheckJob()");
            var result = _service.CheckJob(id: id);

            if (result != null)
            {
                Console.WriteLine("CheckJob() succeeded:\n{0}", JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            else
            {
                Console.WriteLine("Failed to CheckJob()");
            }

            return result;
        }
        #endregion

        #region CheckJobs
        private RecognitionJobs CheckJobs()
        {
            Console.WriteLine("\nAttempting to CheckJobs()");
            var result = _service.CheckJobs();

            if (result != null)
            {
                Console.WriteLine("CheckJobs() succeeded:\n{0}", JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            else
            {
                Console.WriteLine("Failed to CheckJobs()");
            }

            return result;
        }
        #endregion

        #region CreateJob
        private RecognitionJob CreateJob(byte[] audio, string contentType, string transferEncoding = null, string model = null, string callbackUrl = null, string events = null, string userToken = null, long? resultsTtl = null, string customizationId = null, string acousticCustomizationId = null, string baseModelVersion = null, double? customizationWeight = null, long? inactivityTimeout = null, List<string> keywords = null, float? keywordsThreshold = null, long? maxAlternatives = null, float? wordAlternativesThreshold = null, bool? wordConfidence = null, bool? timestamps = null, bool? profanityFilter = null, bool? smartFormatting = null, bool? speakerLabels = null)
        {
            Console.WriteLine("\nAttempting to CreateJob()");
            var result = _service.CreateJob(audio: audio, contentType: contentType, transferEncoding: transferEncoding, model: model, callbackUrl: callbackUrl, events: events, userToken: userToken, resultsTtl: resultsTtl, customizationId: customizationId, acousticCustomizationId: acousticCustomizationId, baseModelVersion: baseModelVersion, customizationWeight: customizationWeight, inactivityTimeout: inactivityTimeout, keywords: keywords, keywordsThreshold: keywordsThreshold, maxAlternatives: maxAlternatives, wordAlternativesThreshold: wordAlternativesThreshold, wordConfidence: wordConfidence, timestamps: timestamps, profanityFilter: profanityFilter, smartFormatting: smartFormatting, speakerLabels: speakerLabels);

            if (result != null)
            {
                Console.WriteLine("CreateJob() succeeded:\n{0}", JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            else
            {
                Console.WriteLine("Failed to CreateJob()");
            }

            return result;
        }
        #endregion

        #region DeleteJob
        private object DeleteJob(string id)
        {
            Console.WriteLine("\nAttempting to DeleteJob()");
            var result = _service.DeleteJob(id: id);

            if (result != null)
            {
                Console.WriteLine("DeleteJob() succeeded:\n{0}", JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            else
            {
                Console.WriteLine("Failed to DeleteJob()");
            }

            return result;
        }
        #endregion

        #region RegisterCallback
        private RegisterStatus RegisterCallback(string callbackUrl, string userSecret = null)
        {
            Console.WriteLine("\nAttempting to RegisterCallback()");
            var result = _service.RegisterCallback(callbackUrl: callbackUrl, userSecret: userSecret);

            if (result != null)
            {
                Console.WriteLine("RegisterCallback() succeeded:\n{0}", JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            else
            {
                Console.WriteLine("Failed to RegisterCallback()");
            }

            return result;
        }
        #endregion

        #region UnregisterCallback
        private object UnregisterCallback(string callbackUrl)
        {
            Console.WriteLine("\nAttempting to UnregisterCallback()");
            var result = _service.UnregisterCallback(callbackUrl: callbackUrl);

            if (result != null)
            {
                Console.WriteLine("UnregisterCallback() succeeded:\n{0}", JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            else
            {
                Console.WriteLine("Failed to UnregisterCallback()");
            }

            return result;
        }
        #endregion

        #region CreateLanguageModel
        private LanguageModel CreateLanguageModel(CreateLanguageModel createLanguageModel)
        {
            Console.WriteLine("\nAttempting to CreateLanguageModel()");
            var result = _service.CreateLanguageModel(createLanguageModel: createLanguageModel);

            if (result != null)
            {
                Console.WriteLine("CreateLanguageModel() succeeded:\n{0}", JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            else
            {
                Console.WriteLine("Failed to CreateLanguageModel()");
            }

            return result;
        }
        #endregion

        #region DeleteLanguageModel
        private object DeleteLanguageModel(string customizationId)
        {
            Console.WriteLine("\nAttempting to DeleteLanguageModel()");
            var result = _service.DeleteLanguageModel(customizationId: customizationId);

            if (result != null)
            {
                Console.WriteLine("DeleteLanguageModel() succeeded:\n{0}", JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            else
            {
                Console.WriteLine("Failed to DeleteLanguageModel()");
            }

            return result;
        }
        #endregion

        #region GetLanguageModel
        private LanguageModel GetLanguageModel(string customizationId)
        {
            Console.WriteLine("\nAttempting to GetLanguageModel()");
            var result = _service.GetLanguageModel(customizationId: customizationId);

            if (result != null)
            {
                Console.WriteLine("GetLanguageModel() succeeded:\n{0}", JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            else
            {
                Console.WriteLine("Failed to GetLanguageModel()");
            }

            return result;
        }
        #endregion

        #region ListLanguageModels
        private LanguageModels ListLanguageModels(string language = null)
        {
            Console.WriteLine("\nAttempting to ListLanguageModels()");
            var result = _service.ListLanguageModels(language: language);

            if (result != null)
            {
                Console.WriteLine("ListLanguageModels() succeeded:\n{0}", JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            else
            {
                Console.WriteLine("Failed to ListLanguageModels()");
            }

            return result;
        }
        #endregion

        #region ResetLanguageModel
        private object ResetLanguageModel(string customizationId)
        {
            Console.WriteLine("\nAttempting to ResetLanguageModel()");
            var result = _service.ResetLanguageModel(customizationId: customizationId);

            if (result != null)
            {
                Console.WriteLine("ResetLanguageModel() succeeded:\n{0}", JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            else
            {
                Console.WriteLine("Failed to ResetLanguageModel()");
            }

            return result;
        }
        #endregion

        #region TrainLanguageModel
        private object TrainLanguageModel(string customizationId, string wordTypeToAdd = null, double? customizationWeight = null)
        {
            Console.WriteLine("\nAttempting to TrainLanguageModel()");
            var result = _service.TrainLanguageModel(customizationId: customizationId, wordTypeToAdd: wordTypeToAdd, customizationWeight: customizationWeight);

            if (result != null)
            {
                Console.WriteLine("TrainLanguageModel() succeeded:\n{0}", JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            else
            {
                Console.WriteLine("Failed to TrainLanguageModel()");
            }

            return result;
        }
        #endregion

        #region UpgradeLanguageModel
        private object UpgradeLanguageModel(string customizationId)
        {
            Console.WriteLine("\nAttempting to UpgradeLanguageModel()");
            var result = _service.UpgradeLanguageModel(customizationId: customizationId);

            if (result != null)
            {
                Console.WriteLine("UpgradeLanguageModel() succeeded:\n{0}", JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            else
            {
                Console.WriteLine("Failed to UpgradeLanguageModel()");
            }

            return result;
        }
        #endregion

        #region AddCorpus
        private object AddCorpus(string customizationId, string corpusName, System.IO.Stream corpusFile, bool? allowOverwrite = null, string corpusFileContentType = null)
        {
            Console.WriteLine("\nAttempting to AddCorpus()");
            var result = _service.AddCorpus(customizationId: customizationId, corpusName: corpusName, corpusFile: corpusFile, allowOverwrite: allowOverwrite, corpusFileContentType: corpusFileContentType);

            if (result != null)
            {
                Console.WriteLine("AddCorpus() succeeded:\n{0}", JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            else
            {
                Console.WriteLine("Failed to AddCorpus()");
            }

            return result;
        }
        #endregion

        #region DeleteCorpus
        private object DeleteCorpus(string customizationId, string corpusName)
        {
            Console.WriteLine("\nAttempting to DeleteCorpus()");
            var result = _service.DeleteCorpus(customizationId: customizationId, corpusName: corpusName);

            if (result != null)
            {
                Console.WriteLine("DeleteCorpus() succeeded:\n{0}", JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            else
            {
                Console.WriteLine("Failed to DeleteCorpus()");
            }

            return result;
        }
        #endregion

        #region GetCorpus
        private Corpus GetCorpus(string customizationId, string corpusName)
        {
            Console.WriteLine("\nAttempting to GetCorpus()");
            var result = _service.GetCorpus(customizationId: customizationId, corpusName: corpusName);

            if (result != null)
            {
                Console.WriteLine("GetCorpus() succeeded:\n{0}", JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            else
            {
                Console.WriteLine("Failed to GetCorpus()");
            }

            return result;
        }
        #endregion

        #region ListCorpora
        private Corpora ListCorpora(string customizationId)
        {
            Console.WriteLine("\nAttempting to ListCorpora()");
            var result = _service.ListCorpora(customizationId: customizationId);

            if (result != null)
            {
                Console.WriteLine("ListCorpora() succeeded:\n{0}", JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            else
            {
                Console.WriteLine("Failed to ListCorpora()");
            }

            return result;
        }
        #endregion

        #region AddWord
        private object AddWord(string customizationId, string wordName, CustomWord customWord)
        {
            Console.WriteLine("\nAttempting to AddWord()");
            var result = _service.AddWord(customizationId: customizationId, wordName: wordName, customWord: customWord);

            if (result != null)
            {
                Console.WriteLine("AddWord() succeeded:\n{0}", JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            else
            {
                Console.WriteLine("Failed to AddWord()");
            }

            return result;
        }
        #endregion

        #region AddWords
        private object AddWords(string customizationId, CustomWords customWords)
        {
            Console.WriteLine("\nAttempting to AddWords()");
            var result = _service.AddWords(customizationId: customizationId, customWords: customWords);

            if (result != null)
            {
                Console.WriteLine("AddWords() succeeded:\n{0}", JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            else
            {
                Console.WriteLine("Failed to AddWords()");
            }

            return result;
        }
        #endregion

        #region DeleteWord
        private object DeleteWord(string customizationId, string wordName)
        {
            Console.WriteLine("\nAttempting to DeleteWord()");
            var result = _service.DeleteWord(customizationId: customizationId, wordName: wordName);

            if (result != null)
            {
                Console.WriteLine("DeleteWord() succeeded:\n{0}", JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            else
            {
                Console.WriteLine("Failed to DeleteWord()");
            }

            return result;
        }
        #endregion

        #region GetWord
        private Word GetWord(string customizationId, string wordName)
        {
            Console.WriteLine("\nAttempting to GetWord()");
            var result = _service.GetWord(customizationId: customizationId, wordName: wordName);

            if (result != null)
            {
                Console.WriteLine("GetWord() succeeded:\n{0}", JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            else
            {
                Console.WriteLine("Failed to GetWord()");
            }

            return result;
        }
        #endregion

        #region ListWords
        private Words ListWords(string customizationId, string wordType = null, string sort = null)
        {
            Console.WriteLine("\nAttempting to ListWords()");
            var result = _service.ListWords(customizationId: customizationId, wordType: wordType, sort: sort);

            if (result != null)
            {
                Console.WriteLine("ListWords() succeeded:\n{0}", JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            else
            {
                Console.WriteLine("Failed to ListWords()");
            }

            return result;
        }
        #endregion

        #region CreateAcousticModel
        private AcousticModel CreateAcousticModel(CreateAcousticModel createAcousticModel)
        {
            Console.WriteLine("\nAttempting to CreateAcousticModel()");
            var result = _service.CreateAcousticModel(createAcousticModel: createAcousticModel);

            if (result != null)
            {
                Console.WriteLine("CreateAcousticModel() succeeded:\n{0}", JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            else
            {
                Console.WriteLine("Failed to CreateAcousticModel()");
            }

            return result;
        }
        #endregion

        #region DeleteAcousticModel
        private object DeleteAcousticModel(string customizationId)
        {
            Console.WriteLine("\nAttempting to DeleteAcousticModel()");
            var result = _service.DeleteAcousticModel(customizationId: customizationId);

            if (result != null)
            {
                Console.WriteLine("DeleteAcousticModel() succeeded:\n{0}", JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            else
            {
                Console.WriteLine("Failed to DeleteAcousticModel()");
            }

            return result;
        }
        #endregion

        #region GetAcousticModel
        private AcousticModel GetAcousticModel(string customizationId)
        {
            Console.WriteLine("\nAttempting to GetAcousticModel()");
            var result = _service.GetAcousticModel(customizationId: customizationId);

            if (result != null)
            {
                Console.WriteLine("GetAcousticModel() succeeded:\n{0}", JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            else
            {
                Console.WriteLine("Failed to GetAcousticModel()");
            }

            return result;
        }
        #endregion

        #region ListAcousticModels
        private AcousticModels ListAcousticModels(string language = null)
        {
            Console.WriteLine("\nAttempting to ListAcousticModels()");
            var result = _service.ListAcousticModels(language: language);

            if (result != null)
            {
                Console.WriteLine("ListAcousticModels() succeeded:\n{0}", JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            else
            {
                Console.WriteLine("Failed to ListAcousticModels()");
            }

            return result;
        }
        #endregion

        #region ResetAcousticModel
        private object ResetAcousticModel(string customizationId)
        {
            Console.WriteLine("\nAttempting to ResetAcousticModel()");
            var result = _service.ResetAcousticModel(customizationId: customizationId);

            if (result != null)
            {
                Console.WriteLine("ResetAcousticModel() succeeded:\n{0}", JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            else
            {
                Console.WriteLine("Failed to ResetAcousticModel()");
            }

            return result;
        }
        #endregion

        #region TrainAcousticModel
        private object TrainAcousticModel(string customizationId, string customLanguageModelId = null)
        {
            Console.WriteLine("\nAttempting to TrainAcousticModel()");
            var result = _service.TrainAcousticModel(customizationId: customizationId, customLanguageModelId: customLanguageModelId);

            if (result != null)
            {
                Console.WriteLine("TrainAcousticModel() succeeded:\n{0}", JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            else
            {
                Console.WriteLine("Failed to TrainAcousticModel()");
            }

            return result;
        }
        #endregion

        #region UpgradeAcousticModel
        private object UpgradeAcousticModel(string customizationId, string customLanguageModelId = null)
        {
            Console.WriteLine("\nAttempting to UpgradeAcousticModel()");
            var result = _service.UpgradeAcousticModel(customizationId: customizationId, customLanguageModelId: customLanguageModelId);

            if (result != null)
            {
                Console.WriteLine("UpgradeAcousticModel() succeeded:\n{0}", JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            else
            {
                Console.WriteLine("Failed to UpgradeAcousticModel()");
            }

            return result;
        }
        #endregion

        #region AddAudio
        private object AddAudio(string customizationId, string audioName, byte[] audioResource, string contentType, string containedContentType = null, bool? allowOverwrite = null)
        {
            Console.WriteLine("\nAttempting to AddAudio()");
            var result = _service.AddAudio(customizationId: customizationId, audioName: audioName, audioResource: audioResource, contentType: contentType, containedContentType: containedContentType, allowOverwrite: allowOverwrite);

            if (result != null)
            {
                Console.WriteLine("AddAudio() succeeded:\n{0}", JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            else
            {
                Console.WriteLine("Failed to AddAudio()");
            }

            return result;
        }
        #endregion

        #region DeleteAudio
        private object DeleteAudio(string customizationId, string audioName)
        {
            Console.WriteLine("\nAttempting to DeleteAudio()");
            var result = _service.DeleteAudio(customizationId: customizationId, audioName: audioName);

            if (result != null)
            {
                Console.WriteLine("DeleteAudio() succeeded:\n{0}", JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            else
            {
                Console.WriteLine("Failed to DeleteAudio()");
            }

            return result;
        }
        #endregion

        #region GetAudio
        private AudioListing GetAudio(string customizationId, string audioName)
        {
            Console.WriteLine("\nAttempting to GetAudio()");
            var result = _service.GetAudio(customizationId: customizationId, audioName: audioName);

            if (result != null)
            {
                Console.WriteLine("GetAudio() succeeded:\n{0}", JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            else
            {
                Console.WriteLine("Failed to GetAudio()");
            }

            return result;
        }
        #endregion

        #region ListAudio
        private AudioResources ListAudio(string customizationId)
        {
            Console.WriteLine("\nAttempting to ListAudio()");
            var result = _service.ListAudio(customizationId: customizationId);

            if (result != null)
            {
                Console.WriteLine("ListAudio() succeeded:\n{0}", JsonConvert.SerializeObject(result, Formatting.Indented));
            }
            else
            {
                Console.WriteLine("Failed to ListAudio()");
            }

            return result;
        }
        #endregion

        #endregion

        private void CheckCustomizationStatus(string classifierId)
        {
            var getLangaugeModelResult = _service.GetLanguageModel(classifierId);

            Console.WriteLine(string.Format("Classifier status is {0}", getLangaugeModelResult.Status));

            if (getLangaugeModelResult.Status == LanguageModel.StatusEnum.READY || getLangaugeModelResult.Status == LanguageModel.StatusEnum.AVAILABLE)
                autoEvent.Set();
            else
            {
                Task.Factory.StartNew(() =>
                {
                    System.Threading.Thread.Sleep(5000);
                    CheckCustomizationStatus(classifierId);
                });
            }
        }

        private void CheckCorpusStatus(string classifierId, string corpusName)
        {
            var getCorpusResult = _service.GetCorpus(classifierId, corpusName);

            Console.WriteLine(string.Format("Corpus status is {0}", getCorpusResult.Status));

            if (getCorpusResult.Status == Corpus.StatusEnum.BEING_PROCESSED)
            {
                Task.Factory.StartNew(() =>
                {
                    System.Threading.Thread.Sleep(5000);
                    CheckCorpusStatus(classifierId, corpusName);
                });
            }
            else if (getCorpusResult.Status == Corpus.StatusEnum.UNDETERMINED)
            {
                throw new Exception("Corpus status is undetermined.");
            }
            else
            {
                autoEvent.Set();
            }
        }

        private void CheckAcousticCustomizationStatus(string classifierId)
        {
            var getAcousticModelResult = _service.GetAcousticModel(classifierId);

            Console.WriteLine(string.Format("Classifier status is {0}", getAcousticModelResult.Status));

            if (getAcousticModelResult.Status == AcousticModel.StatusEnum.AVAILABLE || getAcousticModelResult.Status == AcousticModel.StatusEnum.READY)
                autoEvent.Set();
            else
            {
                Task.Factory.StartNew(() =>
                {
                    System.Threading.Thread.Sleep(5000);
                    CheckAcousticCustomizationStatus(classifierId);
                });
            }
        }

        private void CheckAudioStatus(string classifierId, string audioname)
        {
            var getAudioResult = _service.GetAudio(classifierId, audioname);

            Console.WriteLine(string.Format("Classifier status is {0}", getAudioResult.Status));

            if (getAudioResult.Status == AudioListing.StatusEnum.OK)
            {
                autoEvent.Set();
            }
            else if (getAudioResult.Status == AudioListing.StatusEnum.INVALID)
            {
                throw new Exception("Adding audio failed");
            }
            else
            {
                Task.Factory.StartNew(() =>
                {
                    System.Threading.Thread.Sleep(5000);
                    CheckAcousticCustomizationStatus(classifierId);
                });
            }
        }

        public async Task<byte[]> DownloadAcousticResource(string acousticResourceUrl)
        {
            var client = new HttpClient();
            var task = client.GetByteArrayAsync(acousticResourceUrl);
            var msg = await task;

            return msg;
        }
    }
}
