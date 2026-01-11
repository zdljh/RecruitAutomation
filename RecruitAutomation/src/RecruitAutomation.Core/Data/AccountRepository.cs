using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using RecruitAutomation.Core.Constants;
using RecruitAutomation.Core.Models;

namespace RecruitAutomation.Core.Data
{
    /// <summary>
    /// 账号数据仓库 - 修复持久化与并发问题
    /// </summary>
    public class AccountRepository
    {
        private readonly string _filePath;
        private readonly ReaderWriterLockSlim _lock = new();

        public AccountRepository()
        {
            _filePath = Path.Combine(AppConstants.DataRootPath, "accounts.json");
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        public List<AccountItem> GetAll()
        {
            _lock.EnterReadLock();
            try
            {
                if (!File.Exists(_filePath)) return new List<AccountItem>();
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<List<AccountItem>>(json) ?? new List<AccountItem>();
            }
            catch
            {
                return new List<AccountItem>();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void SaveAll(List<AccountItem> accounts)
        {
            _lock.EnterWriteLock();
            try
            {
                var json = JsonSerializer.Serialize(accounts, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void AddOrUpdate(AccountItem account)
        {
            var accounts = GetAll();
            var index = accounts.FindIndex(a => a.Id == account.Id);
            if (index >= 0)
            {
                accounts[index] = account;
            }
            else
            {
                accounts.Add(account);
            }
            SaveAll(accounts);
        }

        public void Delete(string accountId)
        {
            var accounts = GetAll();
            accounts.RemoveAll(a => a.Id == accountId);
            SaveAll(accounts);
        }
    }
}
