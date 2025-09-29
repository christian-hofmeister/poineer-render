// Jenkins Declarative Pipeline for POIneer.Render
// - Builds & tests on 'develop'
// - Placeholder "deploy" on 'release/*'
// - Avoids heavy rendering on Jenkins
// - Uses repository script to ensure dotnet SDK if missing

pipeline {
  agent any

  options {
    ansiColor('xterm')
    timestamps()
    disableConcurrentBuilds()
    buildDiscarder(logRotator(numToKeepStr: '20', artifactNumToKeepStr: '10'))
    timeout(time: 30, unit: 'MINUTES')
  }

  environment {
    // Speed up dotnet CLI
    DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    NUGET_PACKAGES = "${WORKSPACE}/.nuget/packages"
    // Desired SDK version for the project; adjust if you bump SDK
    DOTNET_SDK_VERSION = '9.0.305'
    // Project paths
    RENDER_CSProj = 'src/POIneer.Render/POIneer.Render.csproj'
    PUBLISH_DIR = 'out/POIneer.Render'
    COVERAGE_MIN = '25'   // später z. B. auf 40/50/60 anheben
  }

  stages {

    stage('Checkout') {
      steps {
        checkout scm
        sh 'git --no-pager log -1 --pretty=fuller'
      }
    }

    stage('Debug dotnet') {
      steps {
        sh '''
          set -eux
          which -a dotnet || true
          echo "PATH=$PATH"
          echo "DOTNET_ROOT=${DOTNET_ROOT-}"
          dotnet --version || true
          dotnet --version
          dotnet --list-sdks
        '''
      }
    }

    stage('Restore') {
      steps {
        sh '''
          set -eux
          mkdir -p "${NUGET_PACKAGES}"
          dotnet restore "${RENDER_CSProj}" --nologo
        '''
      }
    }

    stage('Build') {
      steps {
        sh '''
          set -eux
          dotnet build "${RENDER_CSProj}" -c Release --no-restore --nologo
        '''
      }
    }

    stage('Test') {
      steps {
        sh """
          set -eux
          echo "BRANCH_NAME=\${BRANCH_NAME:-?}"

          # Feste Zielpfade für Coverage
          COV_DIR="TestResults/Coverage"
          COV_FILE="$COV_DIR/coverage.cobertura.xml"
          mkdir -p "$COV_DIR"

          if [ -f POIneerRender.sln ]; then
            dotnet test POIneerRender.sln -c Release --nologo \
              /p:CollectCoverage=true \
              /p:CoverletOutputFormat=cobertura \
              /p:CoverletOutput="$COV_DIR/coverage" \
              /p:Threshold=\${COVERAGE_MIN} /p:ThresholdType=line /p:ThresholdStat=total \
              --logger "junit;LogFileName=test-results.junit.xml"
          else
            PROJECTS="\$(find tests -type f -name '*Tests.csproj' | sort || true)"
            if [ -z "\$PROJECTS" ]; then
              echo "[info] No test projects - skipping."
              exit 0
            fi
            for p in \$PROJECTS; do
              # Für jedes Testprojekt separate Reports zulassen (werden zusammen eingesammelt)
              OUT_DIR="\$(dirname "\$p")/TestResults/Coverage"
              mkdir -p "\$OUT_DIR"
              dotnet test "\$p" -c Release --nologo \
                /p:CollectCoverage=true \
                /p:CoverletOutputFormat=cobertura \
                /p:CoverletOutput="\$OUT_DIR/coverage" \
                /p:Threshold=\${COVERAGE_MIN} /p:ThresholdType=line /p:ThresholdStat=total \
                --logger "junit;LogFileName=test-results.junit.xml"
            done
          fi
        """
      }
      post {
        always {
          junit '**/TestResults/**/test-results.junit.xml'
          // Coverage-Plugin liest Cobertura-XMLs ein
          recordCoverage(
            tools: [[parser: 'COBERTURA', pattern: '**/TestResults/**/coverage.cobertura.xml']],
            sourceCodeRetention: 'LAST_BUILD'
          )
        }
      }
    }

    stage('Coverage Report (HTML)') {
      when { expression { return fileExists('TestResults/Coverage/coverage.cobertura.xml') } }
      steps {
        sh '''
          set -eux
          dotnet tool update -g dotnet-reportgenerator-globaltool || dotnet tool install -g dotnet-reportgenerator-globaltool
          export PATH="$HOME/.dotnet/tools:$PATH"
          reportgenerator \
            -reports:**/TestResults/**/coverage.cobertura.xml \
            -targetdir:CoverageReport \
            -reporttypes:Html
        '''
      }
      post {
        always {
          archiveArtifacts artifacts: 'CoverageReport/**', fingerprint: false
        }
      }
    }



    stage('Publish (App)') {
      steps {
        sh '''
          set -eux
          rm -rf "${PUBLISH_DIR}"
          dotnet publish "${RENDER_CSProj}" -c Release -o "${PUBLISH_DIR}" --no-build --nologo
          tar -C "${PUBLISH_DIR}" -czf "poineer-render_${BRANCH_NAME}.tar.gz" .
        '''
      }
      post {
        success {
          archiveArtifacts artifacts: 'poineer-render_*.tar.gz', fingerprint: true
        }
      }
    }

    stage('Deploy (placeholder)') {
      when {
        expression {
          // Run on branches like release/1.0.0, release/v0.3, etc.
          return env.BRANCH_NAME ==~ /release\/.+/
        }
      }
      steps {
        sh '''
          set -eux
          echo "[deploy] Release branch detected: ${BRANCH_NAME}"
          echo "[deploy] This is a placeholder. No heavy rendering and no live deploy performed on Jenkins."
          echo "[deploy] Here you could: rsync the published app to your render server, trigger a job, etc."
          # Example (commented):
          # rsync -avz --delete "${PUBLISH_DIR}/" user@render-host:/opt/poineer/render/
          # ssh user@render-host 'sudo systemctl restart poineer-render.service'
        '''
      }
    }

    stage('(Optional) Dry-Run Render Check') {
      when {
        allOf {
          branch 'develop'
          expression { return params?.RUN_RENDER_CHECK == true }
        }
      }
      steps {
        sh '''
          set -eux
          echo "[check] Running a short sanity-check invocation (no heavy work expected)."
          # Example of a quick, no-op style run if your app supports it:
          # dotnet "${PUBLISH_DIR}/POIneer.Render.dll" --help
        '''
      }
    }
  }

  parameters {
    // If you ever want to trigger a lightweight health check manually on develop
    booleanParam(name: 'RUN_RENDER_CHECK', defaultValue: false, description: 'Run a quick, no-op sanity check on develop (no heavy rendering).')
  }

  post {
    always {
      script {
        def emoji = currentBuild.currentResult == 'SUCCESS' ? '✅' : (currentBuild.currentResult == 'UNSTABLE' ? '⚠️' : '❌')
        echo "${emoji} Build result: ${currentBuild.currentResult}"
      }
    }
    failure {
      echo "Build failed. See logs above."
    }
  }
}
