using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using LinearCutWpf.Models;
using LinearCutWpf.Services;

namespace LinearCutWpf.Tests
{
    [Collection("Sequential")]
    public class ProfileHeightServiceComprehensiveTests : IDisposable
    {
        private readonly string _testFilePath;
        
        public ProfileHeightServiceComprehensiveTests()
        {
            _testFilePath = Path.GetTempFileName();
            // Переопределяем путь к файлу для тестов
            ProfileHeightService.OverrideFilePath = _testFilePath;
        }
        
        public void Dispose()
        {
            // Удаляем тестовый файл после тестов
            if (File.Exists(_testFilePath))
            {
                File.Delete(_testFilePath);
            }
            // Сбрасываем переопределение пути
            ProfileHeightService.OverrideFilePath = null;
        }
        
        [Fact]
        public void SaveAndLoadProfileHeightsWithMetadata_HandlesManualChangesCorrectly()
        {
            // Arrange - создаем строки с ручными изменениями
            var testRows = new List<ArticleGroupingRow>
            {
                new ArticleGroupingRow 
                { 
                    ArticleName = "ART001", 
                    SelectedVisibleHeight = 1200,
                    IsDefaultValue = false,
                    IsManuallyChanged = true  // Вручную изменено
                },
                new ArticleGroupingRow 
                { 
                    ArticleName = "ART002", 
                    SelectedVisibleHeight = 1000,
                    IsDefaultValue = true,
                    IsManuallyChanged = false  // Значение по умолчанию
                },
                new ArticleGroupingRow 
                { 
                    ArticleName = "ART003", 
                    SelectedVisibleHeight = 1500,
                    IsDefaultValue = false,
                    IsManuallyChanged = true  // Вручную изменено
                }
            };
            
            double? defaultHeight = 1000;
            
            // Act
            ProfileHeightService.SaveProfileHeightsWithMetadata(testRows, defaultHeight);
            
            // Assert
            var loadedData = ProfileHeightService.LoadProfileHeightsWithMetadata();
            
            Assert.NotNull(loadedData);
            Assert.Equal(3, loadedData.Count);
            
            // Проверяем ART001 (вручную изменено)
            Assert.True(loadedData.ContainsKey("ART001"));
            var art001Data = loadedData["ART001"];
            Assert.Equal(1200, art001Data.height);
            Assert.True(art001Data.isManuallyChanged);
            Assert.False(art001Data.isDefaultValue);
            
            // Проверяем ART002 (значение по умолчанию)
            Assert.True(loadedData.ContainsKey("ART002"));
            var art002Data = loadedData["ART002"];
            Assert.Equal(1000, art002Data.height);
            Assert.False(art002Data.isManuallyChanged);
            Assert.True(art002Data.isDefaultValue);
            
            // Проверяем ART003 (вручную изменено)
            Assert.True(loadedData.ContainsKey("ART003"));
            var art003Data = loadedData["ART003"];
            Assert.Equal(1500, art003Data.height);
            Assert.True(art003Data.isManuallyChanged);
            Assert.False(art003Data.isDefaultValue);
            
            var loadedDefaultHeight = ProfileHeightService.LoadDefaultHeight();
            Assert.Equal(defaultHeight, loadedDefaultHeight);
        }
        
        [Fact]
        public void SaveAndLoadProfileHeightsWithMetadata_PreservesManualChangesAfterReload()
        {
            // Arrange - первый набор данных с ручными изменениями
            var initialRows = new List<ArticleGroupingRow>
            {
                new ArticleGroupingRow 
                { 
                    ArticleName = "ART001", 
                    SelectedVisibleHeight = 1200,
                    IsDefaultValue = false,
                    IsManuallyChanged = true  // Вручную изменено
                },
                new ArticleGroupingRow 
                { 
                    ArticleName = "ART002", 
                    SelectedVisibleHeight = 1000,
                    IsDefaultValue = true,
                    IsManuallyChanged = false  // Значение по умолчанию
                }
            };
            
            double? defaultHeight = 1000;
            
            // Act 1 - сохраняем первый раз
            ProfileHeightService.SaveProfileHeightsWithMetadata(initialRows, defaultHeight);
            
            // Act 2 - загружаем и создаем новые строки (имитация перезапуска приложения)
            var loadedData = ProfileHeightService.LoadProfileHeightsWithMetadata();
            var loadedDefault = ProfileHeightService.LoadDefaultHeight();
            
            var reloadedRows = new List<ArticleGroupingRow>();
            foreach (var kvp in loadedData)
            {
                reloadedRows.Add(new ArticleGroupingRow 
                { 
                    ArticleName = kvp.Key, 
                    SelectedVisibleHeight = kvp.Value.height,
                    IsDefaultValue = kvp.Value.isDefaultValue,
                    IsManuallyChanged = kvp.Value.isManuallyChanged
                });
            }
            
            // Проверяем, что при повторном сохранении флаги сохраняются
            ProfileHeightService.SaveProfileHeightsWithMetadata(reloadedRows, loadedDefault);
            
            // Assert - загружаем второй раз и проверяем
            var finalLoadedData = ProfileHeightService.LoadProfileHeightsWithMetadata();
            
            Assert.NotNull(finalLoadedData);
            Assert.Equal(2, finalLoadedData.Count);
            
            // Проверяем ART001 (должен остаться вручную измененным)
            Assert.True(finalLoadedData.ContainsKey("ART001"));
            var art001Data = finalLoadedData["ART001"];
            Assert.Equal(1200, art001Data.height);
            Assert.True(art001Data.isManuallyChanged);
            Assert.False(art001Data.isDefaultValue);
            
            // Проверяем ART002 (должен остаться значением по умолчанию)
            Assert.True(finalLoadedData.ContainsKey("ART002"));
            var art002Data = finalLoadedData["ART002"];
            Assert.Equal(1000, art002Data.height);
            Assert.False(art002Data.isManuallyChanged);
            Assert.True(art002Data.isDefaultValue);
        }
        
        [Fact]
        public void SaveAndLoadProfileHeightsWithMetadata_HandlesZeroAndNegativeHeights()
        {
            // Arrange
            var testRows = new List<ArticleGroupingRow>
            {
                new ArticleGroupingRow 
                { 
                    ArticleName = "ART001", 
                    SelectedVisibleHeight = 0,  // Нулевая высота
                    IsDefaultValue = false,
                    IsManuallyChanged = true
                },
                new ArticleGroupingRow 
                { 
                    ArticleName = "ART002", 
                    SelectedVisibleHeight = -500,  // Отрицательная высота
                    IsDefaultValue = false,
                    IsManuallyChanged = true
                }
            };
            
            double? defaultHeight = 1000;
            
            // Act
            ProfileHeightService.SaveProfileHeightsWithMetadata(testRows, defaultHeight);
            
            // Assert
            var loadedData = ProfileHeightService.LoadProfileHeightsWithMetadata();
            
            Assert.NotNull(loadedData);
            Assert.Equal(2, loadedData.Count);
            
            Assert.True(loadedData.ContainsKey("ART001"));
            var art001Data = loadedData["ART001"];
            Assert.Equal(0, art001Data.height);
            Assert.True(art001Data.isManuallyChanged);
            Assert.False(art001Data.isDefaultValue);
            
            Assert.True(loadedData.ContainsKey("ART002"));
            var art002Data = loadedData["ART002"];
            Assert.Equal(-500, art002Data.height);
            Assert.True(art002Data.isManuallyChanged);
            Assert.False(art002Data.isDefaultValue);
        }
        
        [Fact]
        public void SaveAndLoadProfileHeightsWithMetadata_HandlesDecimalHeights()
        {
            // Arrange
            var testRows = new List<ArticleGroupingRow>
            {
                new ArticleGroupingRow 
                { 
                    ArticleName = "ART001", 
                    SelectedVisibleHeight = 1200.5,  // Дробная высота
                    IsDefaultValue = false,
                    IsManuallyChanged = true
                },
                new ArticleGroupingRow 
                { 
                    ArticleName = "ART002", 
                    SelectedVisibleHeight = 1000.75,  // Дробная высота
                    IsDefaultValue = false,
                    IsManuallyChanged = true
                }
            };
            
            double? defaultHeight = 1000;
            
            // Act
            ProfileHeightService.SaveProfileHeightsWithMetadata(testRows, defaultHeight);
            
            // Assert
            var loadedData = ProfileHeightService.LoadProfileHeightsWithMetadata();
            
            Assert.NotNull(loadedData);
            Assert.Equal(2, loadedData.Count);
            
            Assert.True(loadedData.ContainsKey("ART001"));
            var art001Data = loadedData["ART001"];
            Assert.Equal(1200.5, art001Data.height);
            Assert.True(art001Data.isManuallyChanged);
            Assert.False(art001Data.isDefaultValue);
            
            Assert.True(loadedData.ContainsKey("ART002"));
            var art002Data = loadedData["ART002"];
            Assert.Equal(1000.75, art002Data.height);
            Assert.True(art002Data.isManuallyChanged);
            Assert.False(art002Data.isDefaultValue);
        }
        
        [Fact]
        public void SaveAndLoadProfileHeightsWithMetadata_HandlesSpecialCases()
        {
            // Arrange - тест граничных случаев
            var testRows = new List<ArticleGroupingRow>
            {
                new ArticleGroupingRow 
                { 
                    ArticleName = "ART_WITH_SPACES", 
                    SelectedVisibleHeight = 1200,
                    IsDefaultValue = false,
                    IsManuallyChanged = true
                },
                new ArticleGroupingRow 
                { 
                    ArticleName = "ART_WITH_SPECIAL_CHARS_АБВ", 
                    SelectedVisibleHeight = 1500,
                    IsDefaultValue = false,
                    IsManuallyChanged = true
                },
                new ArticleGroupingRow 
                { 
                    ArticleName = "ART_WITH_NUMBERS_123", 
                    SelectedVisibleHeight = 2000,
                    IsDefaultValue = false,
                    IsManuallyChanged = true
                }
            };
            
            double? defaultHeight = 1000;
            
            // Act
            ProfileHeightService.SaveProfileHeightsWithMetadata(testRows, defaultHeight);
            
            // Assert
            var loadedData = ProfileHeightService.LoadProfileHeightsWithMetadata();
            
            Assert.NotNull(loadedData);
            Assert.Equal(3, loadedData.Count);
            
            Assert.True(loadedData.ContainsKey("ART_WITH_SPACES"));
            Assert.True(loadedData.ContainsKey("ART_WITH_SPECIAL_CHARS_АБВ"));
            Assert.True(loadedData.ContainsKey("ART_WITH_NUMBERS_123"));
            
            var art1Data = loadedData["ART_WITH_SPACES"];
            Assert.Equal(1200, art1Data.height);
            Assert.True(art1Data.isManuallyChanged);
            
            var art2Data = loadedData["ART_WITH_SPECIAL_CHARS_АБВ"];
            Assert.Equal(1500, art2Data.height);
            Assert.True(art2Data.isManuallyChanged);
            
            var art3Data = loadedData["ART_WITH_NUMBERS_123"];
            Assert.Equal(2000, art3Data.height);
            Assert.True(art3Data.isManuallyChanged);
        }
        
        [Fact]
        public void SaveAndLoadProfileHeightsWithMetadata_VerifiesXMLStructure()
        {
            // Arrange
            var testRows = new List<ArticleGroupingRow>
            {
                new ArticleGroupingRow 
                { 
                    ArticleName = "TEST_ART", 
                    SelectedVisibleHeight = 1200,
                    IsDefaultValue = false,
                    IsManuallyChanged = true
                }
            };
            
            double? defaultHeight = 1000;
            
            // Act
            ProfileHeightService.SaveProfileHeightsWithMetadata(testRows, defaultHeight);
            
            // Assert - проверяем структуру XML файла
            Assert.True(File.Exists(_testFilePath));
            
            var xmlContent = File.ReadAllText(_testFilePath);
            Assert.Contains("ProfileHeights", xmlContent);
            Assert.Contains("DefaultHeight=\"1000\"", xmlContent);
            Assert.Contains("Profiles", xmlContent);
            Assert.Contains("Profile", xmlContent);
            Assert.Contains("Article=\"TEST_ART\"", xmlContent);
            Assert.Contains("VisibleHeight=\"1200\"", xmlContent);
            Assert.Contains("IsDefaultValue=\"False\"", xmlContent);
            Assert.Contains("IsManuallyChanged=\"True\"", xmlContent);
        }
        
        [Fact]
        public void LoadProfileHeightsWithMetadata_HandlesCorruptedXML()
        {
            // Arrange - создаем поврежденный XML файл
            var corruptedXml = @"<ProfileHeights DefaultHeight=""1000"">
<Profiles>
<Profile Article=""TEST_ART"" VisibleHeight=""1200"" IsDefaultValue=""False"" IsManuallyChanged=""True"" />
</Profiles>
</ProfileHeights"; // Отсутствует закрывающая скобка
            
            File.WriteAllText(_testFilePath, corruptedXml);
            
            // Act
            var result = ProfileHeightService.LoadProfileHeightsWithMetadata();
            
            // Assert - должен вернуть пустой словарь, а не упасть
            Assert.NotNull(result);
            Assert.Empty(result);
        }
        
        [Fact]
        public void SaveProfileHeightsWithMetadata_HandlesNullCollections()
        {
            // Arrange
            IEnumerable<ArticleGroupingRow> nullRows = null;
            double? defaultHeight = 1000;
            
            // Act
            ProfileHeightService.SaveProfileHeightsWithMetadata(nullRows, defaultHeight);
            
            // Assert - не должно быть исключения
            var loadedData = ProfileHeightService.LoadProfileHeightsWithMetadata();
            Assert.NotNull(loadedData);
            Assert.Empty(loadedData);
            
            var loadedDefault = ProfileHeightService.LoadDefaultHeight();
            Assert.Equal(defaultHeight, loadedDefault);
        }
    }
}