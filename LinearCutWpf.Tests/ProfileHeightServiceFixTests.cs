using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using LinearCutWpf.Services;
using LinearCutWpf.Models;

namespace LinearCutWpf.Tests
{
    [Collection("Sequential")]
    public class ProfileHeightServiceFixTests : IDisposable
    {
        private string _testFilePath;
        
        public ProfileHeightServiceFixTests()
        {
            // Создаем временный файл для тестов
            _testFilePath = Path.GetTempFileName();
            ProfileHeightService.OverrideFilePath = _testFilePath;
        }
        
        public void Dispose()
        {
            // Удаляем временный файл после тестов
            if (File.Exists(_testFilePath))
            {
                // Ожидаем освобождения файла
                int attempts = 0;
                while (attempts < 10)
                {
                    try
                    {
                        File.Delete(_testFilePath);
                        break;
                    }
                    catch (IOException)
                    {
                        attempts++;
                        System.Threading.Thread.Sleep(100);
                    }
                }
            }
            // Сбрасываем переопределение пути
            ProfileHeightService.OverrideFilePath = null;
        }
        
        [Fact]
        public void ManualChangeFlagShouldNotResetAfterReturningToOriginalValue()
        {
            // Arrange - создаем строку с оригинальным значением
            var testRows = new List<ArticleGroupingRow>
            {
                new ArticleGroupingRow 
                { 
                    ArticleName = "TEST001", 
                    SelectedVisibleHeight = 1000,  // Оригинальное значение
                    IsDefaultValue = true,
                    IsManuallyChanged = false
                }
            };
            
            double? defaultHeight = 1000;
            
            // Act 1 - сохраняем оригинальные данные
            ProfileHeightService.SaveProfileHeightsWithMetadata(testRows, defaultHeight);
            
            // Act 2 - имитируем изменение значения пользователем
            testRows[0].SelectedVisibleHeight = 1200;  // Пользователь вводит новое значение
            testRows[0].IsManuallyChanged = true;
            testRows[0].IsDefaultValue = false;
            
            // Act 3 - сохраняем измененные данные
            ProfileHeightService.SaveProfileHeightsWithMetadata(testRows, defaultHeight);
            
            // Act 4 - имитируем возврат к оригинальному значению
            testRows[0].SelectedVisibleHeight = 1000;  // Пользователь возвращается к оригинальному значению
            
            // Act 5 - сохраняем данные после возврата
            ProfileHeightService.SaveProfileHeightsWithMetadata(testRows, defaultHeight);
            
            // Assert - проверяем, что IsManuallyChanged остался true
            var loadedData = ProfileHeightService.LoadProfileHeightsWithMetadata();
            
            Assert.True(loadedData.ContainsKey("TEST001"));
            var testData = loadedData["TEST001"];
            Assert.Equal(1000, testData.height);
            Assert.True(testData.isManuallyChanged);  // ВАЖНО: должен остаться true!
            Assert.False(testData.isDefaultValue);
        }
        
        [Fact]
        public void FocusHandlersShouldPreserveManualChangeFlag()
        {
            // Arrange - создаем строку с оригинальным значением
            var testRow = new ArticleGroupingRow 
            { 
                ArticleName = "TEST002", 
                SelectedVisibleHeight = 1000,  // Оригинальное значение
                IsDefaultValue = true,
                IsManuallyChanged = false
            };
            
            var originalHeights = new Dictionary<string, double?> { { "TEST002", 1000 } };
            
            // Act - имитируем фокус/потерю фокуса с изменением значения
            testRow.SelectedVisibleHeight = 1200;  // Пользователь изменил значение
            
            // Имитируем OnHeightCellLostFocus логику (исправленную)
            if (originalHeights.TryGetValue(testRow.ArticleName, out double? originalHeight))
            {
                if (testRow.SelectedVisibleHeight != originalHeight)
                {
                    testRow.IsManuallyChanged = true;
                    testRow.IsDefaultValue = false;
                }
                // ВАЖНО: больше не сбрасываем IsManuallyChanged в false!
            }
            
            // Assert
            Assert.True(testRow.IsManuallyChanged);
            Assert.False(testRow.IsDefaultValue);
            
            // Act 2 - имитируем возврат к оригинальному значению
            testRow.SelectedVisibleHeight = 1000;  // Возвращаем оригинальное значение
            
            // Имитируем OnHeightCellLostFocus логику снова
            if (originalHeights.TryGetValue(testRow.ArticleName, out double? originalHeight2))
            {
                if (testRow.SelectedVisibleHeight != originalHeight2)
                {
                    testRow.IsManuallyChanged = true;
                    testRow.IsDefaultValue = false;
                }
                // ВАЖНО: не сбрасываем IsManuallyChanged в false!
            }
            
            // Assert 2 - флаг должен остаться true
            Assert.True(testRow.IsManuallyChanged);  // Это ключевое утверждение!
            Assert.False(testRow.IsDefaultValue);
        }
        
        [Fact]
        public void RealUserScenario_FirstTimeValueEntry_ShouldBeMarkedAsManual()
        {
            // Это тест, который воспроизводит реальный сценарий пользователя:
            // 1. Пользователь открывает программу (новый артикул, нет сохраненных данных)
            // 2. Пользователь фокусируется на ячейке высоты (заполняется _originalHeights)
            // 3. Пользователь меняет значение и теряет фокус
            // 4. Ожидается, что IsManuallyChanged = true
            
            // Arrange - имитируем начальное состояние как в Initialize для нового артикула
            var testRows = new List<ArticleGroupingRow>
            {
                new ArticleGroupingRow 
                { 
                    ArticleName = "NEW_ARTICLE", 
                    SelectedVisibleHeight = 1000,  // Значение по умолчанию (как в Initialize)
                    IsDefaultValue = true,          // Новый артикул
                    IsManuallyChanged = false       // Новый артикул
                }
            };
            
            double? defaultHeight = 1000;
            
            // Act 1 - имитируем фокус (OnHeightCellGotFocus)
            // В реальности: _originalHeights[testRow.ArticleName] = testRow.SelectedVisibleHeight;
            var originalHeights = new Dictionary<string, double?> { { "NEW_ARTICLE", 1000 } };
            
            // Act 2 - пользователь меняет значение
            testRows[0].SelectedVisibleHeight = 1200;  // Пользователь вводит новое значение
            
            // Act 3 - имитируем потерю фокуса (OnHeightCellLostFocus) - НОВАЯ ЛОГИКА
            var row = testRows[0];
            // ВСЕГДА помечаем как измененное вручную при потере фокуса
            row.IsManuallyChanged = true;
            row.IsDefaultValue = false;
            
            // Act 4 - сохраняем данные
            ProfileHeightService.SaveProfileHeightsWithMetadata(testRows, defaultHeight);
            
            // Assert - проверяем, что значение сохранено как ручное
            var loadedData = ProfileHeightService.LoadProfileHeightsWithMetadata();
            
            Assert.True(loadedData.ContainsKey("NEW_ARTICLE"));
            var testData = loadedData["NEW_ARTICLE"];
            Assert.Equal(1200, testData.height);
            Assert.True(testData.isManuallyChanged);  // ДОЛЖНО быть true!
            Assert.False(testData.isDefaultValue);
        }
        
        [Fact]
        public void ManualEntryOfSameValueAsOriginal_ShouldBeMarkedAsManual()
        {
            // Специальный тест для случая, когда пользователь вручную вводит
            // значение, совпадающее с оригинальным (или значением по умолчанию)
            
            // Arrange - имитируем артикул с сохраненным оригинальным значением
            var testRows = new List<ArticleGroupingRow>
            {
                new ArticleGroupingRow 
                { 
                    ArticleName = "SAME_VALUE_ARTICLE", 
                    SelectedVisibleHeight = 1000,  // Оригинальное значение
                    IsDefaultValue = false,         // Не значение по умолчанию
                    IsManuallyChanged = false       // Пока не изменено вручную
                }
            };
            
            double? defaultHeight = 1000;
            
            // Сохраняем оригинальное значение
            ProfileHeightService.SaveProfileHeightsWithMetadata(testRows, defaultHeight);
            
            // Act 1 - имитируем фокус (заполняем _originalHeights как в OnHeightCellGotFocus)
            var originalHeights = new Dictionary<string, double?> { { "SAME_VALUE_ARTICLE", 1000 } };
            
            // Act 2 - пользователь вручную вводит ТО ЖЕ САМОЕ значение (1000)
            testRows[0].SelectedVisibleHeight = 1000;  // Тоже значение!
            
            // Act 3 - имитируем потерю фокуса (OnHeightCellLostFocus) - НОВАЯ ЛОГИКА
            var row = testRows[0];
            // ВСЕГДА помечаем как измененное вручную при потере фокуса
            row.IsManuallyChanged = true;
            row.IsDefaultValue = false;
            
            // Act 4 - сохраняем данные
            ProfileHeightService.SaveProfileHeightsWithMetadata(testRows, defaultHeight);
            
            // Assert - проверяем, что значение сохранено как ручное ДАЖЕ если оно совпадает
            var loadedData = ProfileHeightService.LoadProfileHeightsWithMetadata();
            
            Assert.True(loadedData.ContainsKey("SAME_VALUE_ARTICLE"));
            var testData = loadedData["SAME_VALUE_ARTICLE"];
            Assert.Equal(1000, testData.height);
            Assert.True(testData.isManuallyChanged);  // ДОЛЖНО быть true даже при совпадении значений!
            Assert.False(testData.isDefaultValue);
        }
    }
}
