module default {
  type App {
    required property name -> str;
    property repo_url -> str;
    property build_command -> str;
    property run_command -> str;
    property build_folder -> str;
    property required_cpu_cores -> int32;
    property required_memory_mb -> int32;
    property owner_id -> str;
    multi link deployments := .<app[is Deployment];
    multi link instances := .<app[is Instance];
    multi link secrets := .<app[is Secret];
  }

  type Deployment {
    required link app -> App;
    required property status -> str;
    required property created_at -> datetime {
      default := datetime_current();
    }
    property image_tag -> str;
    property port -> int32;
    property owner_id -> str;
  }

  type Instance {
    required link app -> App;
    required link deployment -> Deployment;
    required property container_name -> str {
      constraint exclusive;
    }
    property port -> int32;
    property process_id -> int32;
    property assigned_cpu_cores -> int32;
    property assigned_memory_mb -> int32;
    required property status -> str;
    required property created_at -> datetime {
      default := datetime_current();
    }
    property owner_id -> str;
  }

  type Peer {
    required property name -> str {
      constraint exclusive;
    }
    required property ip_address -> str;
    required property status -> str;
    property tags -> str;
    required property last_seen -> datetime {
      default := datetime_current();
    }
  }

  type Secret {
    required link app -> App;
    required property key -> str;
    required property encrypted_value -> str;
    constraint exclusive on ((.app, .key));
  }
}
