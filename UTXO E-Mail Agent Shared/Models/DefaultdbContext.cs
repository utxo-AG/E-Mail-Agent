using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace UTXO_E_Mail_Agent_Shared.Models;

public partial class DefaultdbContext : DbContext
{
    public DefaultdbContext(DbContextOptions<DefaultdbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Administrator> Administrators { get; set; }

    public virtual DbSet<Agent> Agents { get; set; }

    public virtual DbSet<Conversation> Conversations { get; set; }

    public virtual DbSet<ConversationAttachment> ConversationAttachments { get; set; }

    public virtual DbSet<Customer> Customers { get; set; }

    public virtual DbSet<Logmessage> Logmessages { get; set; }

    public virtual DbSet<Mcpserver> Mcpservers { get; set; }

    public virtual DbSet<Mcpserverrequest> Mcpserverrequests { get; set; }

    public virtual DbSet<Package> Packages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<Administrator>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("administrators");

            entity.HasIndex(e => e.Username, "username").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Loginattempts).HasColumnName("loginattempts");
            entity.Property(e => e.Passwordhash)
                .HasMaxLength(255)
                .HasColumnName("passwordhash");
            entity.Property(e => e.State)
                .HasColumnType("enum('active','blocked')")
                .HasColumnName("state");
            entity.Property(e => e.Username).HasColumnName("username");
        });

        modelBuilder.Entity<Agent>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("agents");

            entity.HasIndex(e => e.CustomerId, "agents_customers");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Aimodel)
                .HasDefaultValueSql("'claude-sonnet-4-20250514'")
                .HasColumnType("enum('claude-sonnet-4-20250514','claude-opus-4-5-20251101','claude-sonnet-4-5-20250929','claude-haiku-3-5')")
                .HasColumnName("aimodel");
            entity.Property(e => e.Aiprovider)
                .HasDefaultValueSql("'claude'")
                .HasColumnType("enum('claude')")
                .HasColumnName("aiprovider");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.Defaultlanguage)
                .HasMaxLength(2)
                .HasColumnName("defaultlanguage");
            entity.Property(e => e.Emailaddress)
                .HasMaxLength(255)
                .HasColumnName("emailaddress");
            entity.Property(e => e.Emailpassword)
                .HasMaxLength(255)
                .HasColumnName("emailpassword");
            entity.Property(e => e.Emailport).HasColumnName("emailport");
            entity.Property(e => e.Emailprovider)
                .HasColumnType("enum('inbound','imap','pop3','exchange')")
                .HasColumnName("emailprovider");
            entity.Property(e => e.Emailprovidertype)
                .HasDefaultValueSql("'polling'")
                .HasColumnType("enum('polling','webhook')")
                .HasColumnName("emailprovidertype");
            entity.Property(e => e.Emailserver)
                .HasMaxLength(255)
                .HasColumnName("emailserver");
            entity.Property(e => e.Emailusername)
                .HasMaxLength(255)
                .HasColumnName("emailusername");
            entity.Property(e => e.Emailusessl).HasColumnName("emailusessl");
            entity.Property(e => e.State)
                .HasColumnType("enum('active','suspend','deleted')")
                .HasColumnName("state");
            entity.Property(e => e.Tasktobecompleted).HasColumnName("tasktobecompleted");

            entity.HasOne(d => d.Customer).WithMany(p => p.Agents)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("agents_customers");
        });

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("conversations");

            entity.HasIndex(e => e.AgentId, "conversations_agents");

            entity.HasIndex(e => e.ConversationreferenceId, "converstions_conversations");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AgentId).HasColumnName("agent_id");
            entity.Property(e => e.Agentresponsehtml).HasColumnName("agentresponsehtml");
            entity.Property(e => e.Agentresponsesubject)
                .HasMaxLength(255)
                .HasColumnName("agentresponsesubject");
            entity.Property(e => e.Agentresponsetext).HasColumnName("agentresponsetext");
            entity.Property(e => e.Aiexplanation).HasColumnName("aiexplanation");
            entity.Property(e => e.ConversationreferenceId).HasColumnName("conversationreference_id");
            entity.Property(e => e.Emailfrom)
                .HasMaxLength(255)
                .HasColumnName("emailfrom");
            entity.Property(e => e.Emailreceived)
                .HasColumnType("datetime")
                .HasColumnName("emailreceived");
            entity.Property(e => e.Htmltext).HasColumnName("htmltext");
            entity.Property(e => e.Messageid)
                .HasMaxLength(255)
                .HasColumnName("messageid");
            entity.Property(e => e.Prompt).HasColumnName("prompt");
            entity.Property(e => e.Subject)
                .HasMaxLength(255)
                .HasColumnName("subject");
            entity.Property(e => e.Text).HasColumnName("text");

            entity.HasOne(d => d.Agent).WithMany(p => p.Conversations)
                .HasForeignKey(d => d.AgentId)
                .HasConstraintName("conversations_agents");

            entity.HasOne(d => d.Conversationreference).WithMany(p => p.InverseConversationreference)
                .HasForeignKey(d => d.ConversationreferenceId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("converstions_conversations");
        });

        modelBuilder.Entity<ConversationAttachment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("conversationAttachments");

            entity.HasIndex(e => e.ConversationId, "conversationattachments_conversations");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.ContentType)
                .HasMaxLength(255)
                .HasColumnName("content_type");
            entity.Property(e => e.ConversationId).HasColumnName("conversation_id");
            entity.Property(e => e.Filename)
                .HasMaxLength(255)
                .HasColumnName("filename");
            entity.Property(e => e.Path)
                .HasMaxLength(255)
                .HasColumnName("path");

            entity.HasOne(d => d.Conversation).WithMany(p => p.ConversationAttachments)
                .HasForeignKey(d => d.ConversationId)
                .HasConstraintName("conversationattachments_conversations");
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("customers");

            entity.HasIndex(e => e.PackageId, "customsers_packages");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.City)
                .HasMaxLength(255)
                .HasColumnName("city");
            entity.Property(e => e.Companyinformation).HasColumnName("companyinformation");
            entity.Property(e => e.Companyname)
                .HasMaxLength(255)
                .HasColumnName("companyname");
            entity.Property(e => e.Country)
                .HasMaxLength(255)
                .HasColumnName("country");
            entity.Property(e => e.Created)
                .HasColumnType("datetime")
                .HasColumnName("created");
            entity.Property(e => e.Emailaddress)
                .HasMaxLength(255)
                .HasColumnName("emailaddress");
            entity.Property(e => e.Firstname)
                .HasMaxLength(255)
                .HasColumnName("firstname");
            entity.Property(e => e.Lastname)
                .HasMaxLength(255)
                .HasColumnName("lastname");
            entity.Property(e => e.PackageId).HasColumnName("package_id");
            entity.Property(e => e.Passwordhash)
                .HasMaxLength(255)
                .HasColumnName("passwordhash");
            entity.Property(e => e.State)
                .HasColumnType("enum('active','notactive','deleted')")
                .HasColumnName("state");
            entity.Property(e => e.Street)
                .HasMaxLength(255)
                .HasColumnName("street");
            entity.Property(e => e.Username)
                .HasMaxLength(255)
                .HasColumnName("username");
            entity.Property(e => e.Zip)
                .HasMaxLength(255)
                .HasColumnName("zip");

            entity.HasOne(d => d.Package).WithMany(p => p.Customers)
                .HasForeignKey(d => d.PackageId)
                .HasConstraintName("customsers_packages");
        });

        modelBuilder.Entity<Logmessage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("logmessages");

            entity.HasIndex(e => e.AgentId, "logmessages_agents");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Additionaldata)
                .HasColumnType("text")
                .HasColumnName("additionaldata");
            entity.Property(e => e.AgentId).HasColumnName("agent_id");
            entity.Property(e => e.Created)
                .HasColumnType("datetime")
                .HasColumnName("created");
            entity.Property(e => e.Message)
                .HasMaxLength(255)
                .HasColumnName("message");

            entity.HasOne(d => d.Agent).WithMany(p => p.Logmessages)
                .HasForeignKey(d => d.AgentId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("logmessages_agents");
        });

        modelBuilder.Entity<Mcpserver>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("mcpserver");

            entity.HasIndex(e => e.AgentId, "mcpserver_agents");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AgentId).HasColumnName("agent_id");
            entity.Property(e => e.Call)
                .HasColumnType("enum('GET','POST','DELETE','UPDATE')")
                .HasColumnName("call");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.Url)
                .HasMaxLength(255)
                .HasColumnName("url");

            entity.HasOne(d => d.Agent).WithMany(p => p.Mcpservers)
                .HasForeignKey(d => d.AgentId)
                .HasConstraintName("mcpserver_agents");
        });

        modelBuilder.Entity<Mcpserverrequest>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("mcpserverrequests");

            entity.HasIndex(e => e.ConversationId, "mcpserverrequests_conversations");

            entity.HasIndex(e => e.McpserverId, "mcpserverrequests_mcpserver");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ConversationId).HasColumnName("conversation_id");
            entity.Property(e => e.Created)
                .HasColumnType("datetime")
                .HasColumnName("created");
            entity.Property(e => e.McpserverId).HasColumnName("mcpserver_id");
            entity.Property(e => e.Parameter)
                .HasColumnType("text")
                .HasColumnName("parameter");
            entity.Property(e => e.Result).HasColumnName("result");

            entity.HasOne(d => d.Conversation).WithMany(p => p.Mcpserverrequests)
                .HasForeignKey(d => d.ConversationId)
                .HasConstraintName("mcpserverrequests_conversations");

            entity.HasOne(d => d.Mcpserver).WithMany(p => p.Mcpserverrequests)
                .HasForeignKey(d => d.McpserverId)
                .HasConstraintName("mcpserverrequests_mcpserver");
        });

        modelBuilder.Entity<Package>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("packages");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.Maxconvsations).HasColumnName("maxconvsations");
            entity.Property(e => e.Price)
                .HasColumnType("double(10,2)")
                .HasColumnName("price");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
