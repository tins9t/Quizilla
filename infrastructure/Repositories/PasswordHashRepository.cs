﻿using Dapper;
using infrastructure.QueryModels;

namespace infrastructure.Repositories;

public class PasswordHashRepository
{
    public void CreatePasswordHash(string userId, string password, string salt)
    {
        var sql = $@"INSERT INTO security(user_id, password_hash, salt) VALUES (@userId, @passwordHash, @salt)";
        using var conn = DataConnection.DataSource.OpenConnection();
        conn.Execute(sql, new { userId, password, salt });
    }

    public void UpdatePassword(string userId, string newPassword, string newSalt)
    {
        var sql = $@"UPDATE security SET password_hash = @password_hash, salt = @salt WHERE user_id = @user_id;";
        using var conn = DataConnection.DataSource.OpenConnection();
        conn.Execute(sql, new
        {
            user_id = userId,
            password_hash = newPassword,
            salt = newSalt
        });
    }

    public PasswordHash GetPasswordHashByUserId(string userId)
    {
        var sql = $@"SELECT * FROM security where user_id = @id;";
        using (var conn = DataConnection.DataSource.OpenConnection())
        {
            return conn.QueryFirst<PasswordHash>(sql, new { id = userId });
        }
    }

    public PasswordHash GetByEmail(string email)
    {
        const string sql = $@"
        SELECT 
        security.user_id as {nameof(PasswordHash.UserId)},
        security.password as {nameof(PasswordHash.Hash)},
        security.salt as {nameof(PasswordHash.Salt)}
        FROM security
        JOIN user ON security.user_id = user.id
        WHERE email = @email;";
        using var connection = DataConnection.DataSource.OpenConnection();
        return connection.QuerySingle<PasswordHash>(sql, new { email });
    }
}