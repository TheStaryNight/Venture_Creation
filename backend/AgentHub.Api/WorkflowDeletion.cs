using Npgsql;

sealed partial class OrbitDatabase
{
    public void DeleteWorkflow(string owner, Guid id)
    {
        using var connection = source!.OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var reminders = new NpgsqlCommand(
            "select status from orbit.reminders where workflow_id=@id and owner_id=@owner for update", connection, transaction);
        reminders.Parameters.AddWithValue("id", id);
        reminders.Parameters.AddWithValue("owner", owner);
        using (var reader = reminders.ExecuteReader())
        {
            while (reader.Read())
                if (reader.GetString(0) == "sending")
                    throw new ActionFailure("Reminder sedang dikirim. Tunggu sampai selesai sebelum menghapus percakapan.");
        }
        using var delete = new NpgsqlCommand("""
            delete from orbit.reminders where workflow_id=@id and owner_id=@owner;
            delete from orbit.workflows where id=@id and owner_id=@owner;
            """, connection, transaction);
        delete.Parameters.AddWithValue("id", id);
        delete.Parameters.AddWithValue("owner", owner);
        delete.ExecuteNonQuery();
        transaction.Commit();
    }
}
